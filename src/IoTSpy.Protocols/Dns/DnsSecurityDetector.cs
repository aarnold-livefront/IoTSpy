namespace IoTSpy.Protocols.Dns;

/// <summary>
/// Lightweight heuristics for spotting DNS traffic that has moved off plaintext port 53 —
/// DNS-over-HTTPS (DoH, RFC 8484) and DNS-over-TLS (DoT, RFC 7858). These are lookalike
/// signals, not protocol decoders: DoH is detected from the HTTP Content-Type already
/// visible on a captured request/response, and DoT from the well-known TLS port. Neither
/// check parses the DNS message carried inside the encrypted channel.
/// </summary>
public static class DnsSecurityDetector
{
    /// <summary>Standard port for DNS-over-TLS (RFC 7858).</summary>
    public const int DotPort = 853;

    private const string DohMessageMediaType = "application/dns-message";
    private const string DohJsonMediaType = "application/dns-json";

    /// <summary>
    /// True when the HTTP Content-Type header indicates DNS-over-HTTPS traffic —
    /// either the wire-format media type (RFC 8484) or the common JSON variant
    /// (Google/Cloudflare-style DoH APIs). Charset/parameter suffixes are ignored.
    /// </summary>
    public static bool IsDohContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return false;

        // Strip any "; charset=..." style parameters before comparing.
        var mediaType = contentType.Split(';')[0].Trim();

        return mediaType.Equals(DohMessageMediaType, StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals(DohJsonMediaType, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True when a destination port matches the well-known DNS-over-TLS port (853).
    /// A cheap heuristic only — it does not confirm the connection is actually
    /// carrying DNS (vs. some other service squatting on 853).
    /// </summary>
    public static bool IsDotPort(int port) => port == DotPort;

    /// <summary>
    /// True when a TLS connection is using the well-known DNS-over-TLS port (853).
    /// Combines the port heuristic with the connection's TLS state so a plaintext
    /// connection to 853 (unusual, but possible) isn't flagged as DoT.
    /// </summary>
    public static bool IsDotConnection(int port, bool isTls) => isTls && IsDotPort(port);
}
