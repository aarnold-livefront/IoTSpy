using IoTSpy.Core.Models;
using Xunit;

namespace IoTSpy.Proxy.Tests;

public class ResilienceOptionsTests
{
    [Fact]
    public void DefaultValues_AreReasonable()
    {
        var opts = new ResilienceOptions();

        Assert.True(opts.ConnectTimeoutSeconds > 0);
        Assert.True(opts.TlsHandshakeTimeoutSeconds > 0);
        Assert.True(opts.RetryCount >= 0);
        Assert.True(opts.RetryBaseDelaySeconds > 0);
        Assert.True(opts.CircuitBreakerFailureRatio > 0 && opts.CircuitBreakerFailureRatio <= 1);
        Assert.True(opts.CircuitBreakerSamplingSeconds > 0);
        Assert.True(opts.CircuitBreakerBreakSeconds > 0);
    }

    [Fact]
    public void SectionName_IsResilience()
    {
        Assert.Equal("Resilience", ResilienceOptions.SectionName);
    }

    [Fact]
    public void DefaultConnectTimeout_Is15Seconds()
    {
        var opts = new ResilienceOptions();
        Assert.Equal(15, opts.ConnectTimeoutSeconds);
    }

    [Fact]
    public void DefaultTlsTimeout_Is10Seconds()
    {
        var opts = new ResilienceOptions();
        Assert.Equal(10, opts.TlsHandshakeTimeoutSeconds);
    }

    [Fact]
    public void DefaultRetryCount_Is2()
    {
        var opts = new ResilienceOptions();
        Assert.Equal(2, opts.RetryCount);
    }

    [Fact]
    public void DefaultCircuitBreakerFailureRatio_Is0Point5()
    {
        var opts = new ResilienceOptions();
        Assert.Equal(0.5, opts.CircuitBreakerFailureRatio);
    }
}
