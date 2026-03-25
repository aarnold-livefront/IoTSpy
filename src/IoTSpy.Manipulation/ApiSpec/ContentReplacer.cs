using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Models;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Manipulation.ApiSpec;

/// <summary>
/// Applies content replacement rules to HTTP responses based on content type,
/// JSON path, header values, or body regex patterns. Supports replacing binary
/// content (images/video/audio) with files from the local filesystem.
/// </summary>
public sealed class ContentReplacer(ILogger<ContentReplacer> logger)
{
    private readonly ConcurrentDictionary<string, byte[]> _fileCache = new();

    /// <summary>
    /// Apply matching replacement rules to the HTTP message. Returns true if any modification was made.
    /// </summary>
    public bool Apply(HttpMessage message, IReadOnlyList<ContentReplacementRule> rules)
    {
        var modified = false;
        var responseContentType = ExtractContentType(message.ResponseHeaders);

        foreach (var rule in rules.Where(r => r.Enabled).OrderBy(r => r.Priority))
        {
            if (!MatchesScope(rule, message))
                continue;

            var applied = rule.MatchType switch
            {
                ContentMatchType.ContentType => ApplyContentTypeRule(rule, message, responseContentType),
                ContentMatchType.JsonPath => ApplyJsonPathRule(rule, message),
                ContentMatchType.HeaderValue => ApplyHeaderRule(rule, message),
                ContentMatchType.BodyRegex => ApplyBodyRegexRule(rule, message),
                _ => false
            };

            if (applied)
            {
                modified = true;
                logger.LogDebug("Applied replacement rule {RuleName} to {Host}{Path}",
                    rule.Name, message.Host, message.Path);
            }
        }

        return modified;
    }

    private static bool MatchesScope(ContentReplacementRule rule, HttpMessage message)
    {
        if (rule.HostPattern is not null &&
            !Regex.IsMatch(message.Host, rule.HostPattern, RegexOptions.IgnoreCase))
            return false;

        if (rule.PathPattern is not null &&
            !Regex.IsMatch(message.Path, rule.PathPattern, RegexOptions.IgnoreCase))
            return false;

        return true;
    }

    private bool ApplyContentTypeRule(ContentReplacementRule rule, HttpMessage message, string responseContentType)
    {
        if (string.IsNullOrWhiteSpace(responseContentType)) return false;

        // Support wildcard matching: "image/*" matches "image/jpeg"
        if (!MatchesContentType(rule.MatchPattern, responseContentType))
            return false;

        return ApplyReplacement(rule, message);
    }

    private bool ApplyJsonPathRule(ContentReplacementRule rule, HttpMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.ResponseBody)) return false;

        try
        {
            var node = JsonNode.Parse(message.ResponseBody);
            if (node is null) return false;

            // Simple JsonPath support: $.field.subfield or $.array[*].field
            var modified = ReplaceJsonPath(node, rule.MatchPattern, rule);
            if (modified)
            {
                message.ResponseBody = node.ToJsonString();
                return true;
            }
        }
        catch (JsonException)
        {
            logger.LogDebug("Response body is not valid JSON for JsonPath rule {RuleName}", rule.Name);
        }

        return false;
    }

    private static bool ApplyHeaderRule(ContentReplacementRule rule, HttpMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.ResponseHeaders)) return false;

        try
        {
            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(message.ResponseHeaders);
            if (headers is null) return false;

            var targetHeader = headers.FirstOrDefault(h =>
                h.Key.Equals(rule.MatchPattern, StringComparison.OrdinalIgnoreCase));

            if (targetHeader.Key is null) return false;

            if (rule.Action == ContentReplacementAction.Redact)
                headers[targetHeader.Key] = "[REDACTED]";
            else if (rule.ReplacementValue is not null)
                headers[targetHeader.Key] = rule.ReplacementValue;
            else
                return false;

            message.ResponseHeaders = JsonSerializer.Serialize(headers);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool ApplyBodyRegexRule(ContentReplacementRule rule, HttpMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.ResponseBody)) return false;

        var replacement = rule.Action == ContentReplacementAction.Redact
            ? "[REDACTED]"
            : rule.ReplacementValue ?? string.Empty;

        var newBody = Regex.Replace(message.ResponseBody, rule.MatchPattern, replacement);
        if (newBody == message.ResponseBody) return false;

        message.ResponseBody = newBody;
        return true;
    }

    private bool ApplyReplacement(ContentReplacementRule rule, HttpMessage message)
    {
        switch (rule.Action)
        {
            case ContentReplacementAction.ReplaceWithFile:
                return ReplaceWithFile(rule, message);

            case ContentReplacementAction.ReplaceWithValue:
                if (rule.ReplacementValue is null) return false;
                message.ResponseBody = rule.ReplacementValue;
                if (rule.ReplacementContentType is not null)
                    UpdateContentType(message, rule.ReplacementContentType);
                return true;

            case ContentReplacementAction.Redact:
                message.ResponseBody = string.Empty;
                return true;

            case ContentReplacementAction.ReplaceWithUrl:
                // For URL replacement, we modify JSON fields pointing to media URLs
                // or set a redirect to the replacement URL
                if (rule.ReplacementValue is null) return false;
                message.ResponseBody = rule.ReplacementValue;
                if (rule.ReplacementContentType is not null)
                    UpdateContentType(message, rule.ReplacementContentType);
                return true;

            default:
                return false;
        }
    }

    private bool ReplaceWithFile(ContentReplacementRule rule, HttpMessage message)
    {
        if (string.IsNullOrWhiteSpace(rule.ReplacementFilePath)) return false;

        try
        {
            var fileBytes = _fileCache.GetOrAdd(rule.ReplacementFilePath, path =>
            {
                if (!File.Exists(path))
                {
                    logger.LogWarning("Replacement file not found: {Path}", path);
                    return [];
                }
                return File.ReadAllBytes(path);
            });

            if (fileBytes.Length == 0) return false;

            // Store as base64 for string-based body transport
            message.ResponseBody = Convert.ToBase64String(fileBytes);

            if (rule.ReplacementContentType is not null)
                UpdateContentType(message, rule.ReplacementContentType);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read replacement file {Path}", rule.ReplacementFilePath);
            return false;
        }
    }

    /// <summary>
    /// Invalidate cached file data (call when files change on disk).
    /// </summary>
    public void InvalidateFileCache(string? filePath = null)
    {
        if (filePath is not null)
            _fileCache.TryRemove(filePath, out _);
        else
            _fileCache.Clear();
    }

    internal static bool MatchesContentType(string pattern, string contentType)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return false;

        // Exact match
        if (pattern.Equals(contentType, StringComparison.OrdinalIgnoreCase))
            return true;

        // Wildcard: "image/*" matches "image/jpeg"
        if (pattern.EndsWith("/*"))
        {
            var prefix = pattern[..^2]; // "image"
            return contentType.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
        }

        // Wildcard: "*/*" matches everything
        if (pattern == "*/*")
            return true;

        return false;
    }

    private static bool ReplaceJsonPath(JsonNode root, string jsonPath, ContentReplacementRule rule)
    {
        // Simple JsonPath: $.field.subfield
        var path = jsonPath.TrimStart('$', '.');
        var segments = path.Split('.');

        return TraverseAndReplace(root, segments, 0, rule);
    }

    private static bool TraverseAndReplace(JsonNode node, string[] segments, int index, ContentReplacementRule rule)
    {
        if (index >= segments.Length) return false;

        var segment = segments[index];
        var isLast = index == segments.Length - 1;

        // Handle array wildcard: [*]
        if (segment.EndsWith("[*]"))
        {
            var fieldName = segment[..^3];
            var target = string.IsNullOrEmpty(fieldName) ? node : node[fieldName];
            if (target is not JsonArray arr) return false;

            var modified = false;
            foreach (var item in arr)
            {
                if (item is null) continue;
                if (isLast)
                {
                    // Can't replace array items directly in this simplified model
                    continue;
                }
                if (TraverseAndReplace(item, segments, index + 1, rule))
                    modified = true;
            }
            return modified;
        }

        if (isLast)
        {
            if (node is not JsonObject obj || !obj.ContainsKey(segment)) return false;

            var replacement = rule.Action == ContentReplacementAction.Redact
                ? "[REDACTED]"
                : rule.ReplacementValue ?? "[REPLACED]";

            obj[segment] = replacement;
            return true;
        }

        var next = node[segment];
        if (next is null) return false;
        return TraverseAndReplace(next, segments, index + 1, rule);
    }

    private static string ExtractContentType(string headers)
    {
        if (string.IsNullOrWhiteSpace(headers)) return string.Empty;
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(headers);
            if (dict is not null)
            {
                foreach (var (key, value) in dict)
                {
                    if (key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                        return value.Split(';')[0].Trim();
                }
            }
        }
        catch { /* ignore */ }
        return string.Empty;
    }

    private static void UpdateContentType(HttpMessage message, string newContentType)
    {
        if (string.IsNullOrWhiteSpace(message.ResponseHeaders)) return;
        try
        {
            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(message.ResponseHeaders);
            if (headers is null) return;

            var ctKey = headers.Keys.FirstOrDefault(k =>
                k.Equals("Content-Type", StringComparison.OrdinalIgnoreCase));

            if (ctKey is not null)
                headers[ctKey] = newContentType;
            else
                headers["Content-Type"] = newContentType;

            message.ResponseHeaders = JsonSerializer.Serialize(headers);
        }
        catch { /* ignore */ }
    }
}
