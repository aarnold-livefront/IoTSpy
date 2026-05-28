using System.Net;
using System.Text.Json;
using IoTSpy.Core.Models;

namespace IoTSpy.Analytics.Features;

public sealed class RequestFeatureExtractor
{
    private static readonly HashSet<int> StandardPorts =
        [80, 443, 8080, 8443, 1883, 8883, 5683, 5684, 53, 5353];

    private static readonly HashSet<string> WeakCiphers =
        ["rc4", "3des", "des", "null", "export", "anon", "anonymous"];

    private static readonly HashSet<string> ModernCiphers =
        ["tls_aes_128_gcm_sha256", "tls_aes_256_gcm_sha384", "tls_chacha20_poly1305_sha256",
         "ecdhe_rsa_aes_128_gcm_sha256", "ecdhe_rsa_aes_256_gcm_sha384"];

    public FeatureVector Extract(CapturedRequest capture)
    {
        var headers = ParseHeaders(capture.RequestHeaders);
        var responseHeaders = ParseHeaders(capture.ResponseHeaders);
        var contentType = GetHeaderValue(responseHeaders, "content-type");
        var ts = capture.Timestamp.ToLocalTime();
        var host = capture.Host ?? string.Empty;

        return new FeatureVector
        {
            ResponseBodySizeLog = MathF.Log(1f + capture.ResponseBodySize),
            RequestBodySizeLog = MathF.Log(1f + capture.RequestBodySize),
            DurationMsLog = MathF.Log(1f + capture.DurationMs),
            Port = capture.Port,
            StatusCode = capture.StatusCode,
            TlsCipherStrength = ClassifyCipher(capture.TlsCipherSuite),
            HourOfDay = ts.Hour,
            DayOfWeek = (float)ts.DayOfWeek,
            DnsNameLength = host.Length,
            DnsNameEntropy = ShannonEntropy(host),
            IsTls = capture.IsTls ? 1f : 0f,
            IsStandardPort = StandardPorts.Contains(capture.Port) ? 1f : 0f,
            IsModified = capture.IsModified ? 1f : 0f,
            HostIsIp = IsIpAddress(host) ? 1f : 0f,
            HasUserAgent = headers.ContainsKey("user-agent") ? 1f : 0f,
            HasAuthorization = headers.ContainsKey("authorization") ? 1f : 0f,
            ContentTypeIsJson = contentType.Contains("json", StringComparison.OrdinalIgnoreCase) ? 1f : 0f,
            ContentTypeIsBinary = IsBinaryContentType(contentType) ? 1f : 0f
        };
    }

    internal static float ShannonEntropy(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0f;
        var freq = s.GroupBy(c => c).Select(g => (float)g.Count() / s.Length);
        return -freq.Sum(p => p * MathF.Log2(p));
    }

    internal static float ClassifyCipher(string cipher)
    {
        if (string.IsNullOrEmpty(cipher)) return 1f;  // unknown
        var lower = cipher.ToLowerInvariant();
        if (WeakCiphers.Any(w => lower.Contains(w))) return 0f;
        if (ModernCiphers.Any(m => lower.Contains(m))) return 2f;
        return 1f;
    }

    private static bool IsIpAddress(string host) =>
        IPAddress.TryParse(host, out _);

    private static bool IsBinaryContentType(string contentType) =>
        contentType.Contains("octet-stream", StringComparison.OrdinalIgnoreCase) ||
        contentType.Contains("protobuf", StringComparison.OrdinalIgnoreCase) ||
        contentType.Contains("binary", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string> ParseHeaders(string headersJson)
    {
        if (string.IsNullOrEmpty(headersJson)) return new(StringComparer.OrdinalIgnoreCase);
        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson) ?? [];
            return new Dictionary<string, string>(raw, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string GetHeaderValue(Dictionary<string, string> headers, string key) =>
        headers.TryGetValue(key, out var v) ? v : string.Empty;
}
