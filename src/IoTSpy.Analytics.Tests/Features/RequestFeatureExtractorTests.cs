using IoTSpy.Analytics.Features;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Models;
using Xunit;

namespace IoTSpy.Analytics.Tests.Features;

public class RequestFeatureExtractorTests
{
    private readonly RequestFeatureExtractor _sut = new();

    private static CapturedRequest Make(Action<CapturedRequest>? configure = null)
    {
        var c = new CapturedRequest
        {
            Id = Guid.NewGuid(),
            Method = "GET",
            Scheme = "https",
            Host = "example.com",
            Port = 443,
            StatusCode = 200,
            IsTls = true,
            TlsVersion = "TLSv1.3",
            TlsCipherSuite = "TLS_AES_256_GCM_SHA384",
            Protocol = InterceptionProtocol.Https,
            Timestamp = new DateTimeOffset(2025, 6, 1, 14, 30, 0, TimeSpan.Zero),
            RequestHeaders = """{"User-Agent":"TestAgent"}""",
            ResponseHeaders = """{"Content-Type":"application/json"}"""
        };
        configure?.Invoke(c);
        return c;
    }

    [Fact]
    public void Extract_StandardCapture_ProducesExpectedBooleanFeatures()
    {
        var c = Make();
        var v = _sut.Extract(c);

        Assert.Equal(1f, v.IsTls);
        Assert.Equal(1f, v.IsStandardPort);
        Assert.Equal(0f, v.IsModified);
        Assert.Equal(0f, v.HostIsIp);
        Assert.Equal(1f, v.HasUserAgent);
        Assert.Equal(0f, v.HasAuthorization);
        Assert.Equal(1f, v.ContentTypeIsJson);
        Assert.Equal(0f, v.ContentTypeIsBinary);
    }

    [Fact]
    public void Extract_NonStandardPort_IsStandardPortIsFalse()
    {
        var c = Make(x => x.Port = 9999);
        var v = _sut.Extract(c);
        Assert.Equal(0f, v.IsStandardPort);
    }

    [Fact]
    public void Extract_IpAddressHost_HostIsIpIsTrue()
    {
        var c = Make(x => x.Host = "192.168.1.100");
        var v = _sut.Extract(c);
        Assert.Equal(1f, v.HostIsIp);
    }

    [Fact]
    public void Extract_BinaryContentType_ContentTypeIsBinaryIsTrue()
    {
        var c = Make(x =>
            x.ResponseHeaders = """{"Content-Type":"application/octet-stream"}""");
        var v = _sut.Extract(c);
        Assert.Equal(1f, v.ContentTypeIsBinary);
        Assert.Equal(0f, v.ContentTypeIsJson);
    }

    [Fact]
    public void Extract_LogTransforms_AreNonNegative()
    {
        var c = Make(x =>
        {
            x.ResponseBodySize = 100_000;
            x.RequestBodySize = 1024;
            x.DurationMs = 500;
        });
        var v = _sut.Extract(c);
        Assert.True(v.ResponseBodySizeLog > 0f);
        Assert.True(v.RequestBodySizeLog > 0f);
        Assert.True(v.DurationMsLog > 0f);
    }

    [Fact]
    public void Extract_ZeroBodySize_LogIsZero()
    {
        var c = Make(x => { x.ResponseBodySize = 0; x.RequestBodySize = 0; x.DurationMs = 0; });
        var v = _sut.Extract(c);
        Assert.Equal(0f, v.ResponseBodySizeLog);
        Assert.Equal(0f, v.RequestBodySizeLog);
        Assert.Equal(0f, v.DurationMsLog);
    }

    [Fact]
    public void Extract_WeakCipher_TlsCipherStrengthIsZero()
    {
        var c = Make(x => x.TlsCipherSuite = "TLS_RSA_WITH_RC4_128_MD5");
        var v = _sut.Extract(c);
        Assert.Equal(0f, v.TlsCipherStrength);
    }

    [Fact]
    public void Extract_ModernCipher_TlsCipherStrengthIsTwo()
    {
        var c = Make(x => x.TlsCipherSuite = "TLS_AES_256_GCM_SHA384");
        var v = _sut.Extract(c);
        Assert.Equal(2f, v.TlsCipherStrength);
    }

    [Fact]
    public void Extract_UnknownCipher_TlsCipherStrengthIsOne()
    {
        var c = Make(x => x.TlsCipherSuite = "SOME_UNKNOWN_CIPHER");
        var v = _sut.Extract(c);
        Assert.Equal(1f, v.TlsCipherStrength);
    }

    [Fact]
    public void ShannonEntropy_EmptyString_ReturnsZero()
    {
        Assert.Equal(0f, RequestFeatureExtractor.ShannonEntropy(string.Empty));
    }

    [Fact]
    public void ShannonEntropy_RandomLookingString_ReturnsHighValue()
    {
        // A high-entropy string: all unique characters
        var entropy = RequestFeatureExtractor.ShannonEntropy("aAbBcCdDeEfFgGhH");
        Assert.True(entropy > 3.0f, $"Expected entropy > 3.0, got {entropy}");
    }

    [Fact]
    public void ToArray_ReturnsCorrectCount()
    {
        var c = Make();
        var v = _sut.Extract(c);
        Assert.Equal(FeatureVector.BaseFeatureCount, v.ToArray().Length);
    }
}
