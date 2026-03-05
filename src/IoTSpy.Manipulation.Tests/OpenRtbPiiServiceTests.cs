using System.Text.Json;
using System.Text.Json.Nodes;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Models;
using IoTSpy.Manipulation;
using IoTSpy.Protocols.OpenRtb;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IoTSpy.Manipulation.Tests;

public class OpenRtbPiiServiceTests
{
    [Fact]
    public void IsOpenRtbRequest_BidRequestOnAuctionPath_ReturnsTrue()
    {
        var service = CreateService();
        var body = """{"id":"1","imp":[{"id":"imp1"}],"device":{"ip":"1.2.3.4"}}""";
        Assert.True(service.IsOpenRtbRequest("application/json", "/openrtb2/auction", body));
    }

    [Fact]
    public void IsOpenRtbRequest_BidResponseOnBidPath_ReturnsTrue()
    {
        var service = CreateService();
        var body = """{"id":"1","seatbid":[{"bid":[{"impid":"1","price":2.0}]}]}""";
        Assert.True(service.IsOpenRtbRequest("application/json", "/bid/response", body));
    }

    [Fact]
    public void IsOpenRtbRequest_NonJsonContentType_ReturnsFalse()
    {
        var service = CreateService();
        var body = """{"imp":[{"id":"1"}]}""";
        Assert.False(service.IsOpenRtbRequest("text/html", "/openrtb2/auction", body));
    }

    [Fact]
    public void IsOpenRtbRequest_NonOpenRtbPath_ReturnsFalse()
    {
        var service = CreateService();
        var body = """{"imp":[{"id":"1"}]}""";
        Assert.False(service.IsOpenRtbRequest("application/json", "/api/users", body));
    }

    [Fact]
    public void IsOpenRtbRequest_EmptyBody_ReturnsFalse()
    {
        var service = CreateService();
        Assert.False(service.IsOpenRtbRequest("application/json", "/bid", ""));
    }

    [Fact]
    public void IsOpenRtbRequest_NonJsonBody_ReturnsFalse()
    {
        var service = CreateService();
        Assert.False(service.IsOpenRtbRequest("application/json", "/bid", "not json at all"));
    }

    [Fact]
    public void IsOpenRtbRequest_PrebidPath_ReturnsTrue()
    {
        var service = CreateService();
        var body = """{"id":"1","imp":[{"id":"1"}]}""";
        Assert.True(service.IsOpenRtbRequest("application/json", "/prebid/server", body));
    }

    [Fact]
    public void IsOpenRtbRequest_OrtbPath_ReturnsTrue()
    {
        var service = CreateService();
        var body = """{"id":"1","imp":[{"id":"1"}]}""";
        Assert.True(service.IsOpenRtbRequest("application/json; charset=utf-8", "/ortb/v2/auction", body));
    }

    [Fact]
    public void GetDefaultPolicies_ReturnsExpectedFieldPaths()
    {
        var defaults = OpenRtbPiiService.GetDefaultPolicies();
        Assert.True(defaults.Count >= 14);

        var fieldPaths = defaults.Select(p => p.FieldPath).ToHashSet();
        Assert.Contains("device.ip", fieldPaths);
        Assert.Contains("device.ipv6", fieldPaths);
        Assert.Contains("device.ifa", fieldPaths);
        Assert.Contains("user.id", fieldPaths);
        Assert.Contains("user.buyeruid", fieldPaths);
        Assert.Contains("device.geo.lat", fieldPaths);
        Assert.Contains("device.geo.lon", fieldPaths);
        Assert.Contains("device.ua", fieldPaths);
        Assert.Contains("user.data", fieldPaths);
    }

    [Fact]
    public void GetDefaultPolicies_HasCorrectStrategies()
    {
        var defaults = OpenRtbPiiService.GetDefaultPolicies();
        var byField = defaults.ToDictionary(p => p.FieldPath, p => p.Strategy);

        Assert.Equal(PiiRedactionStrategy.TruncateIp, byField["device.ip"]);
        Assert.Equal(PiiRedactionStrategy.Redact, byField["device.ifa"]);
        Assert.Equal(PiiRedactionStrategy.HashSha256, byField["user.id"]);
        Assert.Equal(PiiRedactionStrategy.GeneralizeGeo, byField["device.geo.lat"]);
        Assert.Equal(PiiRedactionStrategy.GeneralizeUserAgent, byField["device.ua"]);
        Assert.Equal(PiiRedactionStrategy.Remove, byField["device.didsha1"]);
    }

    private static OpenRtbPiiService CreateService()
    {
        // Lightweight service creation for IsOpenRtbRequest tests (no repos needed)
        return new OpenRtbPiiService(
            new OpenRtbDecoder(),
            new FakeServiceScopeFactory(),
            NullLogger<OpenRtbPiiService>.Instance);
    }

    /// <summary>
    /// Minimal fake for tests that only call IsOpenRtbRequest (no DB access).
    /// </summary>
    private class FakeServiceScopeFactory : Microsoft.Extensions.DependencyInjection.IServiceScopeFactory
    {
        public Microsoft.Extensions.DependencyInjection.IServiceScope CreateScope() =>
            throw new NotSupportedException("Test does not require scoped services");
    }
}
