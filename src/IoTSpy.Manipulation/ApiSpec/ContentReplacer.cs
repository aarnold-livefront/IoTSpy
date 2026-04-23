using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Models;
using IoTSpy.Manipulation.ApiSpec.BodySources;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Manipulation.ApiSpec;

/// <summary>
/// Applies content replacement rules to HTTP responses based on content type,
/// JSON path, header values, or body regex patterns. Supports replacing binary
/// content (images/video/audio) with files from the local filesystem by emitting
/// an <see cref="Core.Interfaces.IResponseBodySource"/> that the proxy writer
/// consumes in preference to the string-based ResponseBody.
/// </summary>
public sealed class ContentReplacer(ILogger<ContentReplacer> logger)
{
    private readonly ConcurrentDictionary<string, FileMetadata> _metaCache = new();

    /// <summary>
    /// Apply matching replacement rules to the HTTP message. Returns true if any modification was made.
    /// </summary>
    public async Task<bool> ApplyAsync(
        HttpMessage message,
        IReadOnlyList<ContentReplacementRule> rules,
        CancellationToken ct = default)
    {
        var modified = false;
        var responseContentType = ExtractContentType(message.ResponseHeaders);

        foreach (var rule in rules.Where(r => r.Enabled).OrderBy(r => r.Priority))
        {
            if (!MatchesScope(rule, message))
                continue;

            var applied = rule.MatchType switch
            {
                ContentMatchType.ContentType => await ApplyContentTypeRuleAsync(rule, message, responseContentType, ct),
                ContentMatchType.JsonPath => ApplyJsonPathRule(rule, message),
                ContentMatchType.HeaderValue => ApplyHeaderRule(rule, message),
                ContentMatchType.BodyRegex => ApplyBodyRegexRule(rule, message),
                _ => false,
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

    private async Task<bool> ApplyContentTypeRuleAsync(
        ContentReplacementRule rule, HttpMessage message, string responseContentType, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(responseContentType)) return false;

        // Support wildcard matching: "image/*" matches "image/jpeg"
        if (!MatchesContentType(rule.MatchPattern, responseContentType))
            return false;

        return await ApplyReplacementAsync(rule, message, ct);
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

    private async Task<bool> ApplyReplacementAsync(ContentReplacementRule rule, HttpMessage message, CancellationToken ct)
    {
        switch (rule.Action)
        {
            case ContentReplacementAction.ReplaceWithFile:
                return await ReplaceWithFileAsync(rule, message, ct);

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
                // or set a redirect to the replacement URL.
                if (rule.ReplacementValue is null) return false;
                message.ResponseBody = rule.ReplacementValue;
                if (rule.ReplacementContentType is not null)
                    UpdateContentType(message, rule.ReplacementContentType);
                return true;

            case ContentReplacementAction.TrackingPixel:
                message.ResponseBodySource = TrackingPixelBodySource.Instance;
                UpdateContentType(message, TrackingPixelBodySource.Instance.ContentType);
                return true;

            case ContentReplacementAction.MockSseStream:
                return MockSseStream(rule, message);

            default:
                return false;
        }
    }

    private bool MockSseStream(ContentReplacementRule rule, HttpMessage message)
    {
        if (string.IsNullOrWhiteSpace(rule.ReplacementFilePath) || !File.Exists(rule.ReplacementFilePath))
        {
            logger.LogWarning("SSE replay file not found: {Path}", rule.ReplacementFilePath);
            return false;
        }

        var delay = rule.SseInterEventDelayMs ?? 0;
        var loop = rule.SseLoop ?? false;
        message.ResponseBodySource = new SseStreamBodySource(rule.ReplacementFilePath, delay, loop);
        message.StatusCode = 200;
        UpdateContentType(message, "text/event-stream");
        return true;
    }

    private async Task<bool> ReplaceWithFileAsync(ContentReplacementRule rule, HttpMessage message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rule.ReplacementFilePath)) return false;

        var path = rule.ReplacementFilePath;
        FileMetadata meta;
        try
        {
            meta = GetFileMetadata(path);
        }
        catch (FileNotFoundException)
        {
            logger.LogWarning("Replacement file not found: {Path}", path);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stat replacement file {Path}", path);
            return false;
        }

        if (meta.Size == 0) return false;

        var mime = rule.ReplacementContentType ?? meta.InferredMime;
        var isBinary = MimeInferrer.IsBinary(mime);

        // Range request for binary asset → serve 206 partial content.
        if (isBinary)
        {
            var rangeHeader = TryGetRequestHeader(message, "Range");
            if (!string.IsNullOrEmpty(rangeHeader) &&
                RangeHelper.TryParse(rangeHeader, meta.Size, out var slice))
            {
                message.ResponseBodySource = new RangeSlicedBodySource(path, slice.Start, slice.End, meta.Size, mime);
                message.StatusCode = 206;
                UpdateContentType(message, mime);
                return true;
            }

            message.ResponseBodySource = new FileStreamBodySource(path, mime, meta.Size);
            message.StatusCode = 200;
            UpdateContentType(message, mime);
            return true;
        }

        // Text file: load as UTF-8 string (legacy path). Small enough to be safe.
        try
        {
            message.ResponseBody = await File.ReadAllTextAsync(path, ct);
            UpdateContentType(message, mime);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read replacement file {Path}", path);
            return false;
        }
    }

    /// <summary>
    /// Invalidate cached file metadata (call when files change on disk).
    /// </summary>
    public void InvalidateFileCache(string? filePath = null)
    {
        if (filePath is not null)
            _metaCache.TryRemove(filePath, out _);
        else
            _metaCache.Clear();
    }

    internal static bool MatchesContentType(string pattern, string contentType)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return false;

        // Exact match
        if (pattern.Equals(contentType, StringComparison.OrdinalIgnoreCase))
            return true;

        // Wildcard: "*/*" matches everything (check before EndsWith("/*") which also matches)
        if (pattern == "*/*")
            return true;

        // Wildcard: "image/*" matches "image/jpeg"
        if (pattern.EndsWith("/*"))
        {
            var prefix = pattern[..^2]; // "image"
            return contentType.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
        }

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

        // Try JSON dict form first (used internally by unit tests and some rule paths)
        if (headers.TrimStart().StartsWith('{'))
        {
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
            catch { /* ignore and fall through to CRLF parse */ }
        }

        // CRLF-separated form (how the proxy delivers headers into HttpMessage)
        foreach (var line in headers.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            var name = line[..colon].Trim();
            if (!name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) continue;
            return line[(colon + 1)..].Split(';')[0].Trim();
        }

        return string.Empty;
    }

    private static void UpdateContentType(HttpMessage message, string newContentType)
    {
        if (string.IsNullOrWhiteSpace(message.ResponseHeaders))
        {
            message.ResponseHeaders = $"Content-Type: {newContentType}";
            return;
        }

        // JSON dict form (tests, JSON-based paths)
        if (message.ResponseHeaders.TrimStart().StartsWith('{'))
        {
            try
            {
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(message.ResponseHeaders);
                if (headers is not null)
                {
                    var ctKey = headers.Keys.FirstOrDefault(k =>
                        k.Equals("Content-Type", StringComparison.OrdinalIgnoreCase));

                    if (ctKey is not null)
                        headers[ctKey] = newContentType;
                    else
                        headers["Content-Type"] = newContentType;

                    message.ResponseHeaders = JsonSerializer.Serialize(headers);
                    return;
                }
            }
            catch { /* fall through to CRLF path */ }
        }

        // CRLF-separated form (real proxy traffic)
        var lines = message.ResponseHeaders.Split(["\r\n"], StringSplitOptions.None).ToList();
        var replaced = false;
        for (var i = 0; i < lines.Count; i++)
        {
            var colon = lines[i].IndexOf(':');
            if (colon <= 0) continue;
            var name = lines[i][..colon].Trim();
            if (!name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) continue;
            lines[i] = $"Content-Type: {newContentType}";
            replaced = true;
            break;
        }
        if (!replaced) lines.Add($"Content-Type: {newContentType}");
        message.ResponseHeaders = string.Join("\r\n", lines.Where(l => !string.IsNullOrWhiteSpace(l)));
    }

    private static string? TryGetRequestHeader(HttpMessage message, string name)
    {
        var hdrs = message.RequestHeaders;
        if (string.IsNullOrWhiteSpace(hdrs)) return null;

        // JSON dict form
        if (hdrs.TrimStart().StartsWith('{'))
        {
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(hdrs);
                if (dict is not null)
                {
                    var match = dict.FirstOrDefault(kv => kv.Key.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (match.Key is not null) return match.Value;
                }
            }
            catch { /* fall through */ }
        }

        foreach (var line in hdrs.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            var hname = line[..colon].Trim();
            if (!hname.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            return line[(colon + 1)..].Trim();
        }
        return null;
    }

    private FileMetadata GetFileMetadata(string path)
    {
        if (!File.Exists(path))
        {
            _metaCache.TryRemove(path, out _);
            throw new FileNotFoundException("Replacement file not found", path);
        }

        var fi = new FileInfo(path);
        if (_metaCache.TryGetValue(path, out var cached) &&
            cached.Size == fi.Length && cached.LastWrite == fi.LastWriteTimeUtc)
        {
            return cached;
        }

        var meta = new FileMetadata(fi.Length, fi.LastWriteTimeUtc, MimeInferrer.FromPath(path));
        _metaCache[path] = meta;
        return meta;
    }

    private readonly record struct FileMetadata(long Size, DateTime LastWrite, string InferredMime);
}
