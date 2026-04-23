using System.Text;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Manipulation.ApiSpec;

/// <summary>
/// Applies a single content replacement rule against a synthetic or captured
/// HTTP exchange without touching the proxy pipeline, returning the modified
/// response plus diagnostic warnings. Used by the rule preview endpoint so
/// researchers can validate rules before going live.
/// </summary>
public sealed class ReplacementPreviewService(
    ContentReplacer contentReplacer,
    IApiSpecRepository specRepo,
    ICaptureRepository captureRepo,
    ILogger<ReplacementPreviewService> logger)
{
    private const int MaxRenderedBytes = 1 * 1024 * 1024; // 1 MB cap
    private const int TextPreviewLimit = 64 * 1024;

    public async Task<PreviewResult?> PreviewAsync(
        Guid specId, Guid ruleId, PreviewRequest request, CancellationToken ct)
    {
        var rule = specId != Guid.Empty
            ? (await specRepo.GetReplacementRulesAsync(specId, ct)).FirstOrDefault(r => r.Id == ruleId)
            : await specRepo.GetRuleByIdAsync(ruleId, ct);
        if (rule is null) return null;

        HttpMessage message;
        var warnings = new List<string>();

        if (request.CaptureId is { } captureId)
        {
            var capture = await captureRepo.GetByIdAsync(captureId, ct);
            if (capture is null)
            {
                warnings.Add($"Capture {captureId} not found; using empty message.");
                message = new HttpMessage { Method = "GET", Host = "unknown", Path = "/" };
            }
            else
            {
                message = CaptureToMessage(capture);
            }
        }
        else if (request.Synthetic is { } syn)
        {
            message = SyntheticToMessage(syn);
        }
        else
        {
            return new PreviewResult(
                Matched: false,
                Modified: false,
                StatusCode: 0,
                ResponseHeaders: [],
                ResponseBodyText: null,
                ResponseBodyBase64: null,
                BodyLength: 0,
                ContentType: null,
                Warnings: ["Request must provide either captureId or synthetic."],
                WasStreamed: false);
        }

        bool modified;
        try
        {
            modified = await contentReplacer.ApplyAsync(message, [rule], ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Preview rule {RuleId} threw during apply", ruleId);
            warnings.Add($"Rule threw: {ex.Message}");
            modified = false;
        }

        var (bodyBytes, wasStreamed) = await RenderBodyAsync(message, warnings, ct);
        var headersDict = ParseResponseHeaders(message.ResponseHeaders);
        var ct_ = headersDict
            .FirstOrDefault(kv => kv.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).Value
            ?? message.ResponseBodySource?.ContentType;

        string? text = null;
        string? base64 = null;
        if (bodyBytes.Length > 0)
        {
            if (bodyBytes.Length <= TextPreviewLimit && IsLikelyText(ct_, bodyBytes))
                text = Encoding.UTF8.GetString(bodyBytes);
            base64 = Convert.ToBase64String(bodyBytes);
        }

        return new PreviewResult(
            Matched: modified,
            Modified: modified,
            StatusCode: message.ResponseBodySource?.StatusCode ?? (message.StatusCode == 0 ? 200 : message.StatusCode),
            ResponseHeaders: headersDict,
            ResponseBodyText: text,
            ResponseBodyBase64: base64,
            BodyLength: bodyBytes.Length,
            ContentType: ct_,
            Warnings: warnings,
            WasStreamed: wasStreamed);
    }

    private static HttpMessage CaptureToMessage(CapturedRequest capture) => new()
    {
        Method = capture.Method,
        Host = capture.Host,
        Port = capture.Port,
        Path = capture.Path,
        Query = capture.Query,
        Scheme = capture.Scheme,
        StatusCode = capture.StatusCode,
        RequestHeaders = capture.RequestHeaders,
        RequestBody = capture.RequestBody,
        ResponseHeaders = capture.ResponseHeaders,
        ResponseBody = capture.ResponseBody,
    };

    private static HttpMessage SyntheticToMessage(SyntheticMessage s)
    {
        var reqHdr = s.RequestHeaders is null
            ? string.Empty
            : System.Text.Json.JsonSerializer.Serialize(s.RequestHeaders);
        var respHdr = s.ResponseHeaders is null
            ? string.Empty
            : System.Text.Json.JsonSerializer.Serialize(s.ResponseHeaders);
        return new HttpMessage
        {
            Method = s.Method ?? "GET",
            Host = s.Host ?? "example.com",
            Path = s.Path ?? "/",
            StatusCode = s.StatusCode ?? 200,
            RequestHeaders = reqHdr,
            RequestBody = s.RequestBody ?? string.Empty,
            ResponseHeaders = respHdr,
            ResponseBody = s.ResponseBody ?? string.Empty,
        };
    }

    private static async Task<(byte[] Bytes, bool Streamed)> RenderBodyAsync(
        HttpMessage message, List<string> warnings, CancellationToken ct)
    {
        if (message.ResponseBodySource is { } src)
        {
            using var ms = new MemoryStream();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            // Streaming sources (SSE, loop) are bounded here — cap by bytes and time.
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            try
            {
                var cappedStream = new CappedStream(ms, MaxRenderedBytes);
                await src.WriteToAsync(cappedStream, cts.Token);
            }
            catch (OperationCanceledException)
            {
                warnings.Add("Body render was cancelled (stream exceeded 2s or 1 MB cap).");
            }
            catch (CappedStream.LimitReachedException)
            {
                warnings.Add($"Response body truncated at {MaxRenderedBytes} bytes.");
            }
            catch (Exception ex)
            {
                warnings.Add($"Body source threw: {ex.Message}");
            }
            return (ms.ToArray(), src.ContentLength is null);
        }
        var bytes = Encoding.UTF8.GetBytes(message.ResponseBody ?? string.Empty);
        return (bytes, false);
    }

    private static Dictionary<string, string> ParseResponseHeaders(string headers)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(headers)) return result;

        if (headers.TrimStart().StartsWith('{'))
        {
            try
            {
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(headers);
                if (dict is not null)
                {
                    foreach (var (k, v) in dict) result[k] = v;
                    return result;
                }
            }
            catch { /* fall through */ }
        }

        foreach (var line in headers.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            result[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }
        return result;
    }

    private static bool IsLikelyText(string? contentType, byte[] bytes)
    {
        if (contentType is not null)
        {
            var ct = contentType.Split(';')[0].Trim();
            if (ct.StartsWith("text/", StringComparison.OrdinalIgnoreCase)) return true;
            if (ct.Equals("application/json", StringComparison.OrdinalIgnoreCase)) return true;
            if (ct.Equals("application/xml", StringComparison.OrdinalIgnoreCase)) return true;
            if (ct.Equals("application/javascript", StringComparison.OrdinalIgnoreCase)) return true;
            if (ct.Equals("application/x-ndjson", StringComparison.OrdinalIgnoreCase)) return true;
        }
        // Heuristic: if no null bytes in the first 512 bytes, treat as text.
        var scan = Math.Min(bytes.Length, 512);
        for (var i = 0; i < scan; i++) if (bytes[i] == 0) return false;
        return scan > 0;
    }

    private sealed class CappedStream(Stream inner, int max) : Stream
    {
        private int _written;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken ct) => inner.FlushAsync(ct);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count)
        {
            var allowed = Math.Min(count, max - _written);
            if (allowed > 0)
            {
                inner.Write(buffer, offset, allowed);
                _written += allowed;
            }
            if (allowed < count) throw new LimitReachedException();
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
        {
            var allowed = Math.Min(buffer.Length, max - _written);
            if (allowed > 0)
            {
                await inner.WriteAsync(buffer[..allowed], ct);
                _written += allowed;
            }
            if (allowed < buffer.Length) throw new LimitReachedException();
        }

        public sealed class LimitReachedException : IOException { }
    }
}

public record PreviewRequest(
    Guid? CaptureId = null,
    SyntheticMessage? Synthetic = null);

public record SyntheticMessage(
    string? Method = "GET",
    string? Host = "example.com",
    string? Path = "/",
    Dictionary<string, string>? RequestHeaders = null,
    string? RequestBody = null,
    int? StatusCode = 200,
    Dictionary<string, string>? ResponseHeaders = null,
    string? ResponseBody = null);

public record PreviewResult(
    bool Matched,
    bool Modified,
    int StatusCode,
    Dictionary<string, string> ResponseHeaders,
    string? ResponseBodyText,
    string? ResponseBodyBase64,
    long BodyLength,
    string? ContentType,
    List<string> Warnings,
    bool WasStreamed);
