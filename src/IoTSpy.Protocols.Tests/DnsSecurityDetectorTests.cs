using Xunit;
using IoTSpy.Protocols.Dns;

namespace IoTSpy.Protocols.Tests;

public class DnsSecurityDetectorTests
{
    // ── DoH content-type heuristic ───────────────────────────────────────────

    [Theory]
    [InlineData("application/dns-message")]
    [InlineData("APPLICATION/DNS-MESSAGE")]
    [InlineData("application/dns-message; charset=utf-8")]
    [InlineData("application/dns-json")]
    [InlineData("application/dns-json; charset=utf-8")]
    public void IsDohContentType_DohMediaTypes_ReturnsTrue(string contentType)
    {
        Assert.True(DnsSecurityDetector.IsDohContentType(contentType));
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("text/html")]
    [InlineData("")]
    [InlineData(null)]
    public void IsDohContentType_NonDohMediaTypes_ReturnsFalse(string? contentType)
    {
        Assert.False(DnsSecurityDetector.IsDohContentType(contentType));
    }

    // ── DoT port heuristic ───────────────────────────────────────────────────

    [Fact]
    public void IsDotPort_Port853_ReturnsTrue()
    {
        Assert.True(DnsSecurityDetector.IsDotPort(853));
    }

    [Theory]
    [InlineData(53)]
    [InlineData(443)]
    [InlineData(8853)]
    public void IsDotPort_OtherPorts_ReturnsFalse(int port)
    {
        Assert.False(DnsSecurityDetector.IsDotPort(port));
    }

    [Fact]
    public void IsDotConnection_TlsOnPort853_ReturnsTrue()
    {
        Assert.True(DnsSecurityDetector.IsDotConnection(853, isTls: true));
    }

    [Fact]
    public void IsDotConnection_PlaintextOnPort853_ReturnsFalse()
    {
        // Port matches but no TLS — not a genuine DoT connection.
        Assert.False(DnsSecurityDetector.IsDotConnection(853, isTls: false));
    }

    [Fact]
    public void IsDotConnection_TlsOnDifferentPort_ReturnsFalse()
    {
        Assert.False(DnsSecurityDetector.IsDotConnection(443, isTls: true));
    }
}
