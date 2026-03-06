using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using IoTSpy.Protocols.OpenRtb;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Manipulation;

/// <summary>
/// Detects OpenRTB traffic, decodes payloads for inspection, and strips PII fields
/// according to configurable policies. Runs inline in the proxy pipeline.
/// </summary>
public sealed class OpenRtbPiiService(
    OpenRtbDecoder decoder,
    IServiceScopeFactory scopeFactory,
    ILogger<OpenRtbPiiService> logger) : IOpenRtbService
{
    private static readonly Regex OpenRtbPathPattern = new(
        @"/(openrtb|ortb|bid|auction|prebid)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private volatile List<OpenRtbPiiPolicy>? _cachedPolicies;
    private DateTimeOffset _cacheExpiry = DateTimeOffset.MinValue;
    private readonly object _cacheLock = new();

    public bool IsOpenRtbRequest(string contentType, string path, string body)
    {
        if (string.IsNullOrEmpty(body)) return false;

        // Content-Type must be JSON
        if (!contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            return false;

        // Path heuristic
        if (!OpenRtbPathPattern.IsMatch(path))
            return false;

        // Body must start with '{' and contain OpenRTB markers
        var trimmed = body.AsSpan().TrimStart();
        if (trimmed.IsEmpty || trimmed[0] != '{') return false;

        var sample = body.AsSpan(0, Math.Min(body.Length, 512));
        return sample.Contains("\"imp\"", StringComparison.Ordinal)
            || sample.Contains("\"seatbid\"", StringComparison.Ordinal);
    }

    public async Task<bool> ProcessAndStripAsync(
        HttpMessage message, ManipulationPhase phase, CancellationToken ct = default)
    {
        var body = phase == ManipulationPhase.Request ? message.RequestBody : message.ResponseBody;
        if (string.IsNullOrEmpty(body)) return false;

        // Decode for inspection and logging
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var decoded = await decoder.DecodeAsync(bodyBytes, ct);
        if (decoded.Count == 0) return false;

        var openRtbMessage = decoded[0];

        // Load policies (cached for performance in RTB path)
        var policies = await GetEnabledPoliciesCachedAsync(ct);
        if (policies.Count == 0)
        {
            // Still log the event even if no stripping policies
            await PersistEventAsync(openRtbMessage, message, phase, ct);
            return false;
        }

        // Parse into mutable JsonNode tree
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return false;
        }

        if (root is not JsonObject rootObj)
            return false;

        var strippingLogs = new List<PiiStrippingLog>();
        var modified = false;

        foreach (var policy in policies)
        {
            ct.ThrowIfCancellationRequested();

            // Check host pattern if configured
            if (policy.HostPattern is not null
                && !Regex.IsMatch(message.Host, policy.HostPattern, RegexOptions.IgnoreCase))
                continue;

            var (node, parent, propertyName) = NavigateToField(rootObj, policy.FieldPath);
            if (node is null || parent is null || propertyName is null)
                continue;

            var originalValue = node.ToJsonString();
            var redacted = ApplyStrategy(policy.Strategy, node, policy.FieldPath);

            if (redacted)
            {
                modified = true;

                strippingLogs.Add(new PiiStrippingLog
                {
                    CapturedRequestId = Guid.Empty, // Set later when capture is recorded
                    Host = message.Host,
                    Path = message.Path,
                    FieldPath = policy.FieldPath,
                    Strategy = policy.Strategy,
                    OriginalValueHash = HashValue(originalValue),
                    RedactedPreview = GetRedactedPreview(originalValue),
                    Phase = phase
                });

                if (policy.Strategy == PiiRedactionStrategy.Remove)
                {
                    parent.AsObject().Remove(propertyName);
                }
            }
        }

        if (modified)
        {
            var newBody = rootObj.ToJsonString();

            if (phase == ManipulationPhase.Request)
                message.RequestBody = newBody;
            else
                message.ResponseBody = newBody;

            // Update Content-Length header
            UpdateContentLength(message, phase, newBody);

            logger.LogDebug("Stripped {Count} PII fields from OpenRTB {Type} to {Host}{Path}",
                strippingLogs.Count, openRtbMessage.MessageType, message.Host, message.Path);
        }

        // Persist event and logs asynchronously (fire-and-forget to not block RTB flow)
        _ = Task.Run(() => PersistEventAndLogsAsync(openRtbMessage, message, phase, strippingLogs, ct), ct);

        return modified;
    }

    public async Task<IReadOnlyList<OpenRtbPiiPolicy>> GetPoliciesAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOpenRtbPiiPolicyRepository>();
        return await repo.GetAllAsync(ct);
    }

    private async Task<List<OpenRtbPiiPolicy>> GetEnabledPoliciesCachedAsync(CancellationToken ct)
    {
        if (_cachedPolicies is not null && DateTimeOffset.UtcNow < _cacheExpiry)
            return _cachedPolicies;

        lock (_cacheLock)
        {
            if (_cachedPolicies is not null && DateTimeOffset.UtcNow < _cacheExpiry)
                return _cachedPolicies;
        }

        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOpenRtbPiiPolicyRepository>();
        var policies = await repo.GetEnabledAsync(ct);

        lock (_cacheLock)
        {
            _cachedPolicies = policies;
            _cacheExpiry = DateTimeOffset.UtcNow.AddSeconds(30);
        }

        return policies;
    }

    private bool ApplyStrategy(PiiRedactionStrategy strategy, JsonNode node, string fieldPath)
    {
        var parent = node.Parent;
        if (parent is not JsonObject parentObj) return false;
        var propName = fieldPath.Contains('.') ? fieldPath[(fieldPath.LastIndexOf('.') + 1)..] : fieldPath;

        switch (strategy)
        {
            case PiiRedactionStrategy.Redact:
                parentObj[propName] = GetRedactedPlaceholder(node, fieldPath);
                return true;

            case PiiRedactionStrategy.TruncateIp:
                if (node is JsonValue val && val.TryGetValue<string>(out var ipStr))
                {
                    parentObj[propName] = TruncateIpAddress(ipStr);
                    return true;
                }
                return false;

            case PiiRedactionStrategy.HashSha256:
                if (node is JsonValue hashVal)
                {
                    var raw = hashVal.ToJsonString().Trim('"');
                    parentObj[propName] = HashValue(raw);
                    return true;
                }
                return false;

            case PiiRedactionStrategy.GeneralizeGeo:
                if (node is JsonValue geoVal && geoVal.TryGetValue<double>(out var coord))
                {
                    parentObj[propName] = Math.Round(coord, 2);
                    return true;
                }
                return false;

            case PiiRedactionStrategy.GeneralizeUserAgent:
                if (node is JsonValue uaVal && uaVal.TryGetValue<string>(out var ua))
                {
                    parentObj[propName] = GeneralizeUserAgent(ua);
                    return true;
                }
                return false;

            case PiiRedactionStrategy.Remove:
                // Removal handled by caller after this returns true
                return true;

            default:
                return false;
        }
    }

    private static JsonNode? GetRedactedPlaceholder(JsonNode node, string fieldPath)
    {
        if (fieldPath.Contains("ip", StringComparison.OrdinalIgnoreCase))
            return "0.0.0.0";
        if (fieldPath.Contains("ifa", StringComparison.OrdinalIgnoreCase))
            return "00000000-0000-0000-0000-000000000000";
        if (node is JsonValue v && v.TryGetValue<double>(out _))
            return 0;
        return "[REDACTED]";
    }

    private static string TruncateIpAddress(string ip)
    {
        // IPv4: truncate last octet
        if (ip.Contains('.'))
        {
            var lastDot = ip.LastIndexOf('.');
            return lastDot >= 0 ? ip[..lastDot] + ".0" : "0.0.0.0";
        }

        // IPv6: truncate last 64 bits (keep first 4 groups)
        if (ip.Contains(':'))
        {
            var parts = ip.Split(':');
            if (parts.Length >= 4)
                return string.Join(":", parts[..4]) + "::";
        }

        return "0.0.0.0";
    }

    private static string GeneralizeUserAgent(string ua)
    {
        // Extract browser and OS family, strip versions
        // "Mozilla/5.0 (Linux; Android 12; Pixel 6) AppleWebKit/537.36 ... Chrome/108.0.5359.128 Mobile Safari/537.36"
        // → "Chrome Android Mobile"
        var parts = new List<string>();

        if (ua.Contains("Chrome", StringComparison.OrdinalIgnoreCase)) parts.Add("Chrome");
        else if (ua.Contains("Firefox", StringComparison.OrdinalIgnoreCase)) parts.Add("Firefox");
        else if (ua.Contains("Safari", StringComparison.OrdinalIgnoreCase)) parts.Add("Safari");
        else if (ua.Contains("Edge", StringComparison.OrdinalIgnoreCase)) parts.Add("Edge");

        if (ua.Contains("Android", StringComparison.OrdinalIgnoreCase)) parts.Add("Android");
        else if (ua.Contains("iOS", StringComparison.OrdinalIgnoreCase) || ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase)) parts.Add("iOS");
        else if (ua.Contains("Windows", StringComparison.OrdinalIgnoreCase)) parts.Add("Windows");
        else if (ua.Contains("Linux", StringComparison.OrdinalIgnoreCase)) parts.Add("Linux");
        else if (ua.Contains("Mac", StringComparison.OrdinalIgnoreCase)) parts.Add("macOS");

        if (ua.Contains("Mobile", StringComparison.OrdinalIgnoreCase)) parts.Add("Mobile");
        if (ua.Contains("Tablet", StringComparison.OrdinalIgnoreCase)) parts.Add("Tablet");
        if (ua.Contains("CTV", StringComparison.OrdinalIgnoreCase) || ua.Contains("SmartTV", StringComparison.OrdinalIgnoreCase)) parts.Add("CTV");

        return parts.Count > 0 ? string.Join(" ", parts) : "Unknown";
    }

    private static string HashValue(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(bytes);
    }

    private static string GetRedactedPreview(string value)
    {
        // Show first 2 and last 2 chars for context
        var clean = value.Trim('"');
        if (clean.Length <= 4) return "***";
        return $"{clean[..2]}...{clean[^2..]}";
    }

    private static (JsonNode? node, JsonNode? parent, string? propertyName) NavigateToField(
        JsonObject root, string fieldPath)
    {
        var parts = fieldPath.Split('.');
        JsonNode? current = root;

        for (var i = 0; i < parts.Length; i++)
        {
            if (current is not JsonObject obj)
                return (null, null, null);

            if (!obj.TryGetPropertyValue(parts[i], out var next) || next is null)
                return (null, null, null);

            if (i == parts.Length - 1)
                return (next, current, parts[i]);

            current = next;
        }

        return (null, null, null);
    }

    private static void UpdateContentLength(HttpMessage message, ManipulationPhase phase, string newBody)
    {
        var headers = phase == ManipulationPhase.Request
            ? message.RequestHeaders
            : message.ResponseHeaders;

        var bodyLength = Encoding.UTF8.GetByteCount(newBody);
        var lines = headers.Split("\r\n").ToList();

        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = $"Content-Length: {bodyLength}";
                break;
            }
        }

        var result = string.Join("\r\n", lines);
        if (phase == ManipulationPhase.Request)
            message.RequestHeaders = result;
        else
            message.ResponseHeaders = result;
    }

    private async Task PersistEventAsync(
        OpenRtbMessage msg, HttpMessage httpMsg, ManipulationPhase phase, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var eventRepo = scope.ServiceProvider.GetRequiredService<IOpenRtbEventRepository>();

            var evt = CreateEvent(msg, httpMsg);
            await eventRepo.AddAsync(evt, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist OpenRTB event");
        }
    }

    private async Task PersistEventAndLogsAsync(
        OpenRtbMessage msg, HttpMessage httpMsg, ManipulationPhase phase,
        List<PiiStrippingLog> logs, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var eventRepo = scope.ServiceProvider.GetRequiredService<IOpenRtbEventRepository>();
            var logRepo = scope.ServiceProvider.GetRequiredService<IPiiStrippingLogRepository>();

            var evt = CreateEvent(msg, httpMsg);
            await eventRepo.AddAsync(evt, ct);

            if (logs.Count > 0)
                await logRepo.AddBatchAsync(logs, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist OpenRTB event/logs");
        }
    }

    private static OpenRtbEvent CreateEvent(OpenRtbMessage msg, HttpMessage httpMsg)
    {
        return new OpenRtbEvent
        {
            Version = msg.Version,
            MessageType = msg.MessageType,
            ImpressionCount = msg.Impressions.Count,
            BidCount = msg.Bids.Count,
            HasDeviceInfo = msg.Device is not null,
            HasUserData = msg.User is not null,
            HasGeoData = msg.Device?.GeoLat is not null,
            Exchange = httpMsg.Host,
            RawJson = msg.RawJson
        };
    }

    /// <summary>
    /// Returns the default PII policies that are seeded on first use.
    /// </summary>
    public static List<OpenRtbPiiPolicy> GetDefaultPolicies() =>
    [
        new() { FieldPath = "device.ip", Strategy = PiiRedactionStrategy.TruncateIp, Priority = 10 },
        new() { FieldPath = "device.ipv6", Strategy = PiiRedactionStrategy.TruncateIp, Priority = 10 },
        new() { FieldPath = "device.ifa", Strategy = PiiRedactionStrategy.Redact, Priority = 10 },
        new() { FieldPath = "device.didsha1", Strategy = PiiRedactionStrategy.Remove, Priority = 20 },
        new() { FieldPath = "device.didmd5", Strategy = PiiRedactionStrategy.Remove, Priority = 20 },
        new() { FieldPath = "device.macsha1", Strategy = PiiRedactionStrategy.Remove, Priority = 20 },
        new() { FieldPath = "device.macmd5", Strategy = PiiRedactionStrategy.Remove, Priority = 20 },
        new() { FieldPath = "user.id", Strategy = PiiRedactionStrategy.HashSha256, Priority = 10 },
        new() { FieldPath = "user.buyeruid", Strategy = PiiRedactionStrategy.HashSha256, Priority = 10 },
        new() { FieldPath = "user.keywords", Strategy = PiiRedactionStrategy.Remove, Priority = 30 },
        new() { FieldPath = "user.data", Strategy = PiiRedactionStrategy.Remove, Priority = 30 },
        new() { FieldPath = "device.geo.lat", Strategy = PiiRedactionStrategy.GeneralizeGeo, Priority = 15 },
        new() { FieldPath = "device.geo.lon", Strategy = PiiRedactionStrategy.GeneralizeGeo, Priority = 15 },
        new() { FieldPath = "device.ua", Strategy = PiiRedactionStrategy.GeneralizeUserAgent, Priority = 25 },
    ];
}
