using IoTSpy.Proxy.Interception;
using Xunit;

namespace IoTSpy.Proxy.Tests;

public class SslStripServiceTests
{
    // ── GetHttpsRedirectLocation ──────────────────────────────────────

    [Theory]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(307)]
    [InlineData(308)]
    public void GetHttpsRedirectLocation_HttpsRedirect_ReturnsUrl(int statusCode)
    {
        var headers = "Location: https://example.com/secure\r\nContent-Length: 0";
        var result = SslStripService.GetHttpsRedirectLocation(statusCode, headers);
        Assert.Equal("https://example.com/secure", result);
    }

    [Fact]
    public void GetHttpsRedirectLocation_HttpLocation_ReturnsNull()
    {
        var headers = "Location: http://example.com/page\r\nContent-Length: 0";
        var result = SslStripService.GetHttpsRedirectLocation(301, headers);
        Assert.Null(result);
    }

    [Fact]
    public void GetHttpsRedirectLocation_NonRedirectStatus_ReturnsNull()
    {
        var headers = "Location: https://example.com/secure\r\nContent-Length: 0";
        var result = SslStripService.GetHttpsRedirectLocation(200, headers);
        Assert.Null(result);
    }

    [Fact]
    public void GetHttpsRedirectLocation_NoLocationHeader_ReturnsNull()
    {
        var headers = "Content-Type: text/html\r\nContent-Length: 0";
        var result = SslStripService.GetHttpsRedirectLocation(301, headers);
        Assert.Null(result);
    }

    [Fact]
    public void GetHttpsRedirectLocation_303Status_ReturnsUrl()
    {
        var headers = "Location: https://example.com/after-post";
        var result = SslStripService.GetHttpsRedirectLocation(303, headers);
        Assert.Equal("https://example.com/after-post", result);
    }

    // ── StripResponseHeaders ──────────────────────────────────────────

    [Fact]
    public void StripResponseHeaders_RemovesHstsHeader()
    {
        var headers = "Content-Type: text/html\r\nStrict-Transport-Security: max-age=31536000\r\nContent-Length: 100";
        var result = SslStripService.StripResponseHeaders(headers);

        Assert.DoesNotContain("Strict-Transport-Security", result);
        Assert.Contains("Content-Type: text/html", result);
        Assert.Contains("Content-Length: 100", result);
    }

    [Fact]
    public void StripResponseHeaders_RewritesLocationHeader()
    {
        var headers = "Location: https://example.com/redirect";
        var result = SslStripService.StripResponseHeaders(headers);

        Assert.Contains("http://example.com/redirect", result);
        Assert.DoesNotContain("https://", result);
    }

    [Fact]
    public void StripResponseHeaders_RewritesSetCookieHeader()
    {
        var headers = "Set-Cookie: session=abc; domain=.example.com; path=/; secure; https://example.com";
        var result = SslStripService.StripResponseHeaders(headers);

        Assert.DoesNotContain("https://", result);
    }

    [Fact]
    public void StripResponseHeaders_RewritesCspHeader()
    {
        var headers = "Content-Security-Policy: default-src https://cdn.example.com";
        var result = SslStripService.StripResponseHeaders(headers);

        Assert.Contains("http://cdn.example.com", result);
        Assert.DoesNotContain("https://", result);
    }

    [Fact]
    public void StripResponseHeaders_LeavesNonTargetHeadersUnchanged()
    {
        var headers = "X-Custom: foo\r\nContent-Type: application/json";
        var result = SslStripService.StripResponseHeaders(headers);

        Assert.Contains("X-Custom: foo", result);
        Assert.Contains("Content-Type: application/json", result);
    }

    // ── StripHttpsFromBody ────────────────────────────────────────────

    [Fact]
    public void StripHttpsFromBody_ReplacesHttpsUrls()
    {
        var body = "<a href=\"https://example.com\">Link</a> and https://api.example.com/data";
        var result = SslStripService.StripHttpsFromBody(body);

        Assert.DoesNotContain("https://", result);
        Assert.Contains("http://example.com", result);
        Assert.Contains("http://api.example.com/data", result);
    }

    [Fact]
    public void StripHttpsFromBody_NoHttps_LeavesUnchanged()
    {
        var body = "<a href=\"http://example.com\">Link</a>";
        var result = SslStripService.StripHttpsFromBody(body);

        Assert.Equal(body, result);
    }

    [Fact]
    public void StripHttpsFromBody_EmptyBody_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SslStripService.StripHttpsFromBody(string.Empty));
    }

    [Fact]
    public void StripHttpsFromBody_JsonBody_RewritesUrls()
    {
        var body = "{\"redirect\":\"https://example.com/api\",\"callback\":\"https://callback.example.com\"}";
        var result = SslStripService.StripHttpsFromBody(body);

        Assert.DoesNotContain("https://", result);
        Assert.Contains("http://example.com/api", result);
    }
}
