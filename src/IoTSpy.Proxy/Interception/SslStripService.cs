using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Proxy.Interception;

/// <summary>
/// Implements SSL stripping: intercepts HTTP→HTTPS redirects, follows the HTTPS
/// URL upstream, and serves the decrypted response back over plain HTTP.
/// Also strips HSTS headers and rewrites https:// links in responses.
/// </summary>
public sealed class SslStripService(ILogger<SslStripService> logger)
{
    /// <summary>
    /// Returns the HTTPS Location URL if the response is a redirect to HTTPS, otherwise null.
    /// </summary>
    public static string? GetHttpsRedirectLocation(int statusCode, string headers)
    {
        if (statusCode is not (301 or 302 or 303 or 307 or 308))
            return null;

        var location = ExtractHeaderValue(headers, "Location");
        if (location is null) return null;

        return location.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? location : null;
    }

    /// <summary>
    /// Fetches a URL over HTTPS, returning the full decrypted HTTP response (status line, headers, body).
    /// </summary>
    public async Task<(string statusLine, string headers, string body, byte[] bodyBytes)?> FetchHttpsAsync(
        string url, string originalRequestHeaders, byte[] originalRequestBody,
        string method, int maxBodyKb, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 443;
        var pathAndQuery = uri.PathAndQuery;

        logger.LogInformation(
            "SSL strip: following HTTPS redirect to {Host}:{Port}{Path}",
            host, port, pathAndQuery);

        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(host, port, ct);

            using var ssl = new SslStream(tcp.GetStream(), leaveInnerStreamOpen: false,
                (_, _, _, _) => true); // Accept all certs (research tool)
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = host,
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            }, ct);

            // Build request — rewrite Host header and request line for the new URL
            var requestLine = $"{method} {pathAndQuery} HTTP/1.1";
            var rewrittenHeaders = RewriteRequestHeaders(originalRequestHeaders, host, port);

            var head = new StringBuilder();
            head.Append(requestLine).Append("\r\n");
            if (!string.IsNullOrEmpty(rewrittenHeaders)) head.Append(rewrittenHeaders).Append("\r\n");
            head.Append("\r\n");

            await ssl.WriteAsync(Encoding.UTF8.GetBytes(head.ToString()), ct);
            if (originalRequestBody.Length > 0)
                await ssl.WriteAsync(originalRequestBody, ct);

            // Read response
            var (statusLine, respHeaders, respBody, respBodyBytes) = await ReadHttpResponseAsync(ssl, maxBodyKb, ct);
            if (statusLine is null)
                return null;

            // Strip HSTS and rewrite https:// in response headers
            respHeaders = StripResponseHeaders(respHeaders);

            // Rewrite https:// → http:// in body for HTML/JSON content
            var contentType = ExtractHeaderValue(respHeaders, "Content-Type") ?? "";
            if (IsTextContent(contentType) && respBody.Length > 0)
            {
                respBody = StripHttpsFromBody(respBody);
                respBodyBytes = Encoding.UTF8.GetBytes(respBody);
                respHeaders = UpdateContentLength(respHeaders, respBodyBytes.Length);
            }

            logger.LogInformation(
                "SSL strip: fetched HTTPS response from {Host}:{Port} — status={StatusLine}, bodySize={BodySize}B",
                host, port, statusLine, respBodyBytes.Length);

            return (statusLine, respHeaders, respBody, respBodyBytes);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SSL strip: failed to fetch HTTPS from {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// Strips Strict-Transport-Security headers, rewrites https:// → http:// in
    /// Location, Set-Cookie, Content-Security-Policy headers.
    /// </summary>
    public static string StripResponseHeaders(string headers)
    {
        var lines = headers.Split("\r\n");
        var result = new List<string>();

        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line)) continue;

            // Remove HSTS header entirely
            if (line.StartsWith("Strict-Transport-Security:", StringComparison.OrdinalIgnoreCase))
                continue;

            // Rewrite https:// → http:// in redirect, cookie, and CSP headers
            if (line.StartsWith("Location:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Set-Cookie:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Content-Security-Policy:", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(line.Replace("https://", "http://"));
                continue;
            }

            result.Add(line);
        }

        return string.Join("\r\n", result);
    }

    /// <summary>
    /// Rewrites https:// → http:// in HTML/JSON/text response bodies.
    /// </summary>
    public static string StripHttpsFromBody(string body)
    {
        return body.Replace("https://", "http://");
    }

    private static string RewriteRequestHeaders(string headers, string newHost, int newPort)
    {
        var lines = headers.Split("\r\n");
        var result = new List<string>();

        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line)) continue;

            if (line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(newPort == 443 ? $"Host: {newHost}" : $"Host: {newHost}:{newPort}");
                continue;
            }

            result.Add(line);
        }

        return string.Join("\r\n", result);
    }

    private static bool IsTextContent(string contentType)
    {
        return contentType.Contains("text/", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("xml", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("javascript", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractHeaderValue(string headers, string headerName)
    {
        foreach (var line in headers.Split("\r\n"))
        {
            if (line.StartsWith(headerName + ":", StringComparison.OrdinalIgnoreCase))
                return line[(headerName.Length + 1)..].Trim();
        }
        return null;
    }

    private static string UpdateContentLength(string headers, int byteCount)
    {
        var lines = headers.Split("\r\n")
            .Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (byteCount > 0)
            lines.Add($"Content-Length: {byteCount}");
        return string.Join("\r\n", lines);
    }

    // Simplified HTTP response reader for SSL strip HTTPS fetch
    private static async Task<(string? statusLine, string headers, string body, byte[] bodyBytes)> ReadHttpResponseAsync(
        Stream stream, int maxBodyKb, CancellationToken ct)
    {
        string? statusLine = null;
        var headerLines = new List<string>();
        int contentLength = 0;
        bool chunked = false;

        while (true)
        {
            var line = await ReadLineAsync(stream, ct);
            if (line is null) return (null, "", "", []);
            if (statusLine is null) { statusLine = line; continue; }
            if (string.IsNullOrEmpty(line)) break;
            headerLines.Add(line);
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                int.TryParse(line[15..].Trim(), out contentLength);
            if (line.Contains("chunked", StringComparison.OrdinalIgnoreCase))
                chunked = true;
        }

        var headers = string.Join("\r\n", headerLines);
        byte[] bodyBytes = [];
        var body = string.Empty;

        try
        {
            if (contentLength > 0)
            {
                var maxBytes = maxBodyKb * 1024;
                var readLen = Math.Min(contentLength, maxBytes);
                bodyBytes = new byte[readLen];
                await stream.ReadExactlyAsync(bodyBytes, ct);
                body = Encoding.UTF8.GetString(bodyBytes);
                if (contentLength > maxBytes)
                {
                    var drain = new byte[contentLength - maxBytes];
                    await stream.ReadExactlyAsync(drain, ct);
                }
            }
            else if (chunked)
            {
                var rawList = new List<byte>();
                var sb = new StringBuilder();
                while (true)
                {
                    var sizeLine = await ReadLineAsync(stream, ct);
                    if (sizeLine is null) break;
                    if (!int.TryParse(sizeLine.Trim().Split(';')[0], System.Globalization.NumberStyles.HexNumber, null, out var chunkSize))
                        break; // Malformed chunk size — stop reading
                    if (chunkSize == 0) { await ReadLineAsync(stream, ct); break; }
                    var buf = new byte[chunkSize];
                    await stream.ReadExactlyAsync(buf, ct);
                    await ReadLineAsync(stream, ct);
                    var maxBytes = maxBodyKb * 1024;
                    if (rawList.Count < maxBytes)
                    {
                        var take = Math.Min(chunkSize, maxBytes - rawList.Count);
                        rawList.AddRange(buf.AsSpan(0, take));
                        sb.Append(Encoding.UTF8.GetString(buf, 0, take));
                    }
                }
                bodyBytes = rawList.ToArray();
                body = sb.ToString();
            }
        }
        catch (EndOfStreamException)
        {
            // Upstream closed before sending complete body — return what we have
        }

        return (statusLine, headers, body, bodyBytes);
    }

    private static async Task<string?> ReadLineAsync(Stream stream, CancellationToken ct)
    {
        var bytes = new List<byte>(256);
        var buf = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(buf.AsMemory(0, 1), ct);
            if (read == 0) return bytes.Count == 0 ? null : Encoding.ASCII.GetString([.. bytes]).TrimEnd('\r');
            if (buf[0] == '\n') return Encoding.ASCII.GetString([.. bytes]).TrimEnd('\r');
            bytes.Add(buf[0]);
        }
    }
}
