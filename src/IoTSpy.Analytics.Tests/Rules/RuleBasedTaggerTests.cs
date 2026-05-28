using IoTSpy.Analytics.Rules;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Models;
using Xunit;

namespace IoTSpy.Analytics.Tests.Rules;

public class RuleBasedTaggerTests
{
    private readonly RuleBasedTagger _sut = new();

    private static CapturedRequest Make(Action<CapturedRequest>? configure = null)
    {
        var c = new CapturedRequest
        {
            Id = Guid.NewGuid(),
            Host = "example.com",
            Port = 443,
            StatusCode = 200,
            IsTls = true,
            TlsVersion = "TLSv1.3",
            TlsCipherSuite = "TLS_AES_256_GCM_SHA384",
            Protocol = InterceptionProtocol.Https,
            Timestamp = DateTimeOffset.UtcNow
        };
        configure?.Invoke(c);
        return c;
    }

    // ── UnusualPort ──────────────────────────────────────────────────────────

    [Fact]
    public void Tag_UnusualPort_WhenPortIsNonStandard()
    {
        var c = Make(x => x.Port = 9999);
        Assert.Contains((RiskTag.UnusualPort, 0.95), _sut.Tag(c));
    }

    [Fact]
    public void Tag_NoUnusualPort_WhenPort443()
    {
        var c = Make(x => x.Port = 443);
        Assert.DoesNotContain(_sut.Tag(c), t => t.Tag == RiskTag.UnusualPort);
    }

    [Theory]
    [InlineData(80), InlineData(443), InlineData(8080), InlineData(1883), InlineData(53)]
    public void IsUnusualPort_StandardPorts_ReturnFalse(int port)
    {
        var c = Make(x => x.Port = port);
        Assert.False(_sut.IsUnusualPort(c));
    }

    // ── SuspiciousTls ────────────────────────────────────────────────────────

    [Fact]
    public void Tag_SuspiciousTls_WhenTls10()
    {
        var c = Make(x => { x.IsTls = true; x.TlsVersion = "TLSv1.0"; });
        Assert.Contains(_sut.Tag(c), t => t.Tag == RiskTag.SuspiciousTls);
    }

    [Fact]
    public void Tag_SuspiciousTls_WhenWeakCipher()
    {
        var c = Make(x => { x.IsTls = true; x.TlsCipherSuite = "TLS_RSA_WITH_RC4_128_MD5"; });
        Assert.Contains(_sut.Tag(c), t => t.Tag == RiskTag.SuspiciousTls);
    }

    [Fact]
    public void Tag_NoSuspiciousTls_WhenNotTls()
    {
        var c = Make(x => { x.IsTls = false; x.TlsVersion = "TLSv1.0"; });
        Assert.DoesNotContain(_sut.Tag(c), t => t.Tag == RiskTag.SuspiciousTls);
    }

    [Fact]
    public void Tag_NoSuspiciousTls_WhenModernTls()
    {
        var c = Make(x => { x.IsTls = true; x.TlsVersion = "TLSv1.3"; x.TlsCipherSuite = "TLS_AES_256_GCM_SHA384"; });
        Assert.DoesNotContain(_sut.Tag(c), t => t.Tag == RiskTag.SuspiciousTls);
    }

    // ── MqttCredentialExposure ───────────────────────────────────────────────

    [Fact]
    public void Tag_MqttCredentialExposure_WhenMqttWithCredentials()
    {
        var c = Make(x =>
        {
            x.Protocol = InterceptionProtocol.Mqtt;
            x.RequestBody = """{"username":"admin","password":"secret"}""";
        });
        Assert.Contains(_sut.Tag(c), t => t.Tag == RiskTag.MqttCredentialExposure);
    }

    [Fact]
    public void Tag_NoMqttCredentialExposure_WhenHttpProtocol()
    {
        var c = Make(x =>
        {
            x.Protocol = InterceptionProtocol.Http;
            x.RequestBody = """{"username":"admin","password":"secret"}""";
        });
        Assert.DoesNotContain(_sut.Tag(c), t => t.Tag == RiskTag.MqttCredentialExposure);
    }

    // ── DnsTunneling ─────────────────────────────────────────────────────────

    [Fact]
    public void Tag_DnsTunneling_WhenVeryLongDnsName()
    {
        var c = Make(x =>
        {
            x.Protocol = InterceptionProtocol.Dns;
            x.Host = new string('a', 70) + ".example.com";
        });
        Assert.Contains(_sut.Tag(c), t => t.Tag == RiskTag.DnsTunneling);
    }

    [Fact]
    public void Tag_DnsTunneling_WhenHighEntropyDnsName()
    {
        // High-entropy: random-looking subdomain used for DNS tunneling
        var c = Make(x =>
        {
            x.Protocol = InterceptionProtocol.Dns;
            x.Host = "xk9pQr2mVzAbYw.evil.com";
        });
        Assert.Contains(_sut.Tag(c), t => t.Tag == RiskTag.DnsTunneling);
    }

    [Fact]
    public void Tag_NoDnsTunneling_WhenHttpProtocol()
    {
        var c = Make(x =>
        {
            x.Protocol = InterceptionProtocol.Http;
            x.Host = "xk9pQr2mVzAbYw.evil.com";
        });
        Assert.DoesNotContain(_sut.Tag(c), t => t.Tag == RiskTag.DnsTunneling);
    }

    // ── DataBroker ───────────────────────────────────────────────────────────

    [Fact]
    public void Tag_DataBroker_WhenHostIsKnownTracker()
    {
        var c = Make(x => x.Host = "analytics.google.com");
        Assert.Contains(_sut.Tag(c), t => t.Tag == RiskTag.DataBroker);
    }

    [Fact]
    public void Tag_DataBroker_WhenHostIsSubdomainOfTracker()
    {
        var c = Make(x => x.Host = "cdn.doubleclick.net");
        Assert.Contains(_sut.Tag(c), t => t.Tag == RiskTag.DataBroker);
    }

    [Fact]
    public void Tag_NoDataBroker_WhenHostIsLegitimate()
    {
        var c = Make(x => x.Host = "api.mydevice.local");
        Assert.DoesNotContain(_sut.Tag(c), t => t.Tag == RiskTag.DataBroker);
    }

    // ── PiiDetected ──────────────────────────────────────────────────────────

    [Fact]
    public void Tag_PiiDetected_WhenEmailInBody()
    {
        var c = Make(x => x.RequestBody = """{"email":"user@example.com","name":"Alice"}""");
        Assert.Contains(_sut.Tag(c), t => t.Tag == RiskTag.PiiDetected);
    }

    [Fact]
    public void Tag_PiiDetected_WhenSsnPatternInBody()
    {
        var c = Make(x => x.ResponseBody = "ssn: 123-45-6789");
        Assert.Contains(_sut.Tag(c), t => t.Tag == RiskTag.PiiDetected);
    }

    [Fact]
    public void Tag_PiiDetected_WhenPasswordKeyInBody()
    {
        var c = Make(x => x.RequestBody = """{"password": "hunter2"}""");
        Assert.Contains(_sut.Tag(c), t => t.Tag == RiskTag.PiiDetected);
    }

    [Fact]
    public void Tag_NoPiiDetected_WhenNoPiiInBody()
    {
        var c = Make(x => x.RequestBody = """{"status":"ok","count":42}""");
        Assert.DoesNotContain(_sut.Tag(c), t => t.Tag == RiskTag.PiiDetected);
    }

    // ── ExfiltrationRisk ─────────────────────────────────────────────────────

    [Fact]
    public void Tag_ExfiltrationRisk_WhenLargeResponseToExternalHost()
    {
        var c = Make(x =>
        {
            x.Host = "uploads.external.io";
            x.ResponseBodySize = 1_000_000;
        });
        Assert.Contains(_sut.Tag(c), t => t.Tag == RiskTag.ExfiltrationRisk);
    }

    [Fact]
    public void Tag_NoExfiltrationRisk_WhenLargeResponseToInternalHost()
    {
        var c = Make(x =>
        {
            x.Host = "192.168.1.50";
            x.ResponseBodySize = 1_000_000;
        });
        Assert.DoesNotContain(_sut.Tag(c), t => t.Tag == RiskTag.ExfiltrationRisk);
    }

    [Fact]
    public void Tag_NoExfiltrationRisk_WhenSmallResponse()
    {
        var c = Make(x =>
        {
            x.Host = "uploads.external.io";
            x.ResponseBodySize = 1024;
        });
        Assert.DoesNotContain(_sut.Tag(c), t => t.Tag == RiskTag.ExfiltrationRisk);
    }

    // ── HighEntropyPayload ────────────────────────────────────────────────────

    [Fact]
    public void Tag_HighEntropyPayload_WhenCleartextWithEncodedBody()
    {
        // A base64-encoded/compressed payload on HTTP (non-TLS)
        var highEntropyBody = Convert.ToBase64String(
            Enumerable.Range(0, 200).Select(i => (byte)(i % 256)).ToArray());
        var c = Make(x =>
        {
            x.IsTls = false;
            x.Protocol = InterceptionProtocol.Http;
            x.RequestBody = highEntropyBody;
        });
        Assert.Contains(_sut.Tag(c), t => t.Tag == RiskTag.HighEntropyPayload);
    }

    [Fact]
    public void Tag_NoHighEntropyPayload_WhenTlsTraffic()
    {
        var highEntropyBody = Convert.ToBase64String(
            Enumerable.Range(0, 200).Select(i => (byte)(i % 256)).ToArray());
        var c = Make(x =>
        {
            x.IsTls = true;
            x.RequestBody = highEntropyBody;
        });
        Assert.DoesNotContain(_sut.Tag(c), t => t.Tag == RiskTag.HighEntropyPayload);
    }

    // ── Clean capture ────────────────────────────────────────────────────────

    [Fact]
    public void Tag_NormalCapture_ReturnsNoTags()
    {
        var c = Make(x =>
        {
            x.Host = "api.mydevice.local";
            x.Port = 443;
            x.IsTls = true;
            x.TlsVersion = "TLSv1.3";
            x.Protocol = InterceptionProtocol.Https;
            x.RequestBody = """{"status":"ok"}""";
            x.ResponseBodySize = 256;
        });
        var tags = _sut.Tag(c);
        Assert.Empty(tags);
    }
}
