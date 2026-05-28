using System.Text;
using System.Text.RegularExpressions;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Models;

namespace IoTSpy.Analytics.Rules;

public sealed partial class RuleBasedTagger
{
    private static readonly HashSet<int> StandardPorts =
        [80, 443, 8080, 8443, 1883, 8883, 5683, 5684, 53, 5353];

    private static readonly HashSet<string> WeakTlsVersions =
        ["TLSv1", "TLSv1.0", "TLSv1.1", "SSL3", "SSLv3"];

    private static readonly string[] WeakCipherSubstrings =
        ["RC4", "3DES", "DES", "NULL", "EXPORT", "ANON", "ANONYMOUS"];

    // PII patterns
    private static readonly Regex EmailPattern = EmailRegex();
    private static readonly Regex SsnPattern = SsnRegex();
    private static readonly Regex GeoPattern = GeoRegex();
    private static readonly Regex CredentialKeyPattern = CredentialKeyRegex();

    private readonly IReadOnlyList<string> _dataBrokerDomains;

    public RuleBasedTagger(IReadOnlyList<string>? dataBrokerDomains = null)
    {
        _dataBrokerDomains = dataBrokerDomains ?? DefaultDataBrokerDomains;
    }

    public IReadOnlyList<(RiskTag Tag, double Confidence)> Tag(CapturedRequest capture)
    {
        var results = new List<(RiskTag, double)>();

        if (IsUnusualPort(capture))
            results.Add((RiskTag.UnusualPort, 0.95));

        if (IsSuspiciousTls(capture))
            results.Add((RiskTag.SuspiciousTls, 0.90));

        if (IsMqttCredentialExposure(capture))
            results.Add((RiskTag.MqttCredentialExposure, 0.95));

        if (IsDnsTunneling(capture))
            results.Add((RiskTag.DnsTunneling, 0.85));

        if (IsDataBroker(capture))
            results.Add((RiskTag.DataBroker, 0.90));

        if (ContainsPii(capture))
            results.Add((RiskTag.PiiDetected, 0.80));

        if (IsExfiltrationRisk(capture))
            results.Add((RiskTag.ExfiltrationRisk, 0.75));

        if (HasHighEntropyPayload(capture))
            results.Add((RiskTag.HighEntropyPayload, 0.80));

        return results;
    }

    internal bool IsUnusualPort(CapturedRequest capture) =>
        capture.Port > 0 && !StandardPorts.Contains(capture.Port);

    internal bool IsSuspiciousTls(CapturedRequest capture)
    {
        if (!capture.IsTls) return false;
        if (WeakTlsVersions.Contains(capture.TlsVersion)) return true;
        if (!string.IsNullOrEmpty(capture.TlsCipherSuite))
        {
            var upper = capture.TlsCipherSuite.ToUpperInvariant();
            return WeakCipherSubstrings.Any(w => upper.Contains(w));
        }
        return false;
    }

    internal bool IsMqttCredentialExposure(CapturedRequest capture)
    {
        if (capture.Protocol is not (InterceptionProtocol.Mqtt or InterceptionProtocol.MqttTls))
            return false;

        var body = capture.RequestBody ?? string.Empty;
        // MQTT CONNECT packets include username/password; look for credential keywords
        return body.Contains("\"username\"", StringComparison.OrdinalIgnoreCase) ||
               body.Contains("\"password\"", StringComparison.OrdinalIgnoreCase) ||
               body.Contains("CONNECT", StringComparison.Ordinal);
    }

    internal bool IsDnsTunneling(CapturedRequest capture)
    {
        if (capture.Protocol is not (InterceptionProtocol.Dns or InterceptionProtocol.MDns))
            return false;

        var host = capture.Host ?? string.Empty;
        if (host.Length > 60) return true;
        return ShannonEntropy(host) > 3.5f;
    }

    internal bool IsDataBroker(CapturedRequest capture)
    {
        var host = (capture.Host ?? string.Empty).ToLowerInvariant();
        return _dataBrokerDomains.Any(domain =>
            host == domain || host.EndsWith("." + domain, StringComparison.Ordinal));
    }

    internal bool ContainsPii(CapturedRequest capture)
    {
        var content = string.Concat(capture.RequestBody, capture.ResponseBody);
        if (string.IsNullOrEmpty(content)) return false;
        return EmailPattern.IsMatch(content) ||
               SsnPattern.IsMatch(content) ||
               CredentialKeyPattern.IsMatch(content) ||
               GeoPattern.IsMatch(content);
    }

    internal bool IsExfiltrationRisk(CapturedRequest capture) =>
        capture.ResponseBodySize > 512_000 && IsExternalHost(capture.Host);

    internal bool HasHighEntropyPayload(CapturedRequest capture)
    {
        if (capture.IsTls) return false;  // encrypted traffic is expected to be high-entropy
        var body = capture.RequestBody ?? string.Empty;
        if (body.Length < 64) return false;
        // 5.5 bits/char threshold: catches base64/compressed content (max ~6.0 for 64-char alphabet)
        // while passing normal JSON/text payloads (~3-4 bits/char)
        return ShannonEntropy(body) > 5.5f;
    }

    private static bool IsExternalHost(string? host)
    {
        if (string.IsNullOrEmpty(host)) return false;
        if (host.StartsWith("192.168.") || host.StartsWith("10.") ||
            host.StartsWith("172.") || host is "localhost" or "127.0.0.1")
            return false;
        return true;
    }

    private static float ShannonEntropy(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0f;
        var freq = s.GroupBy(c => c).Select(g => (float)g.Count() / s.Length);
        return -freq.Sum(p => p * MathF.Log2(p));
    }

    [GeneratedRegex(@"\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\b\d{3}-\d{2}-\d{4}\b")]
    private static partial Regex SsnRegex();

    [GeneratedRegex(@"""(password|token|secret|api_key|apikey|ssn|credit_card|card_number)""\s*:")]
    private static partial Regex CredentialKeyRegex();

    [GeneratedRegex(@"""(lat|latitude|lon|longitude|lng)""\s*:\s*-?\d{1,3}\.\d{3}")]
    private static partial Regex GeoRegex();

    private static readonly IReadOnlyList<string> DefaultDataBrokerDomains =
    [
        "doubleclick.net",
        "googlesyndication.com",
        "googletagmanager.com",
        "googletagservices.com",
        "google-analytics.com",
        "analytics.google.com",
        "crashlytics.com",
        "firebase.google.com",
        "amplitude.com",
        "segment.io",
        "segment.com",
        "mixpanel.com",
        "hotjar.com",
        "fullstory.com",
        "logrocket.com",
        "heap.io",
        "intercom.io",
        "optimizely.com",
        "mparticle.com",
        "braze.com",
        "adjust.com",
        "appsflyer.com",
        "branch.io",
        "comscore.com",
        "quantserve.com",
        "scorecardresearch.com",
        "advertising.com",
        "adnxs.com",
        "adsystem.amazon.com",
        "moatads.com",
        "criteo.com",
        "taboola.com",
        "outbrain.com"
    ];
}
