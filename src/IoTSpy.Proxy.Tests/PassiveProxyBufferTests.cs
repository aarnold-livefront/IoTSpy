using IoTSpy.Core.Models;
using IoTSpy.Proxy.Passive;
using Xunit;

namespace IoTSpy.Proxy.Tests;

public class PassiveProxyBufferTests
{
    private static CapturedRequest MakeCapture(string clientIp = "192.168.1.1", string host = "api.example.com", string path = "/data") =>
        new() { ClientIp = clientIp, Host = host, Path = path, Method = "GET", StatusCode = 200 };

    [Fact]
    public void Add_AddsToBuffer()
    {
        var buf = new PassiveProxyBuffer();
        buf.Add(MakeCapture());
        Assert.Equal(1, buf.Count);
    }

    [Fact]
    public void Add_EvictsOldestWhenFull()
    {
        var buf = new PassiveProxyBuffer();
        for (var i = 0; i < PassiveProxyBuffer.MaxCapacity; i++)
            buf.Add(MakeCapture(host: $"host{i}"));

        var extra = MakeCapture(host: "new-host");
        buf.Add(extra);

        Assert.Equal(PassiveProxyBuffer.MaxCapacity, buf.Count);
        var snapshot = buf.Snapshot();
        Assert.Equal("new-host", snapshot[^1].Host);
        Assert.Equal("host1", snapshot[0].Host); // host0 was evicted
    }

    [Fact]
    public void Clear_EmptiesBuffer()
    {
        var buf = new PassiveProxyBuffer();
        buf.Add(MakeCapture());
        buf.Add(MakeCapture());
        buf.Clear();
        Assert.Equal(0, buf.Count);
    }

    [Fact]
    public void Snapshot_ReturnsCopy()
    {
        var buf = new PassiveProxyBuffer();
        buf.Add(MakeCapture());
        var snap1 = buf.Snapshot();
        buf.Clear();
        Assert.Single(snap1); // snapshot is unaffected by clear
    }

    [Fact]
    public void DeviceFilter_WhenSet_RejectsNonMatchingCaptures()
    {
        var buf = new PassiveProxyBuffer();
        buf.SetDeviceFilter(["10.0.0.1"]);

        buf.Add(MakeCapture(clientIp: "10.0.0.1")); // should be accepted
        buf.Add(MakeCapture(clientIp: "10.0.0.2")); // should be rejected

        Assert.Equal(1, buf.Count);
        Assert.Equal("10.0.0.1", buf.Snapshot()[0].ClientIp);
    }

    [Fact]
    public void DeviceFilter_WhenEmpty_AcceptsAll()
    {
        var buf = new PassiveProxyBuffer();
        buf.Add(MakeCapture(clientIp: "10.0.0.1"));
        buf.Add(MakeCapture(clientIp: "10.0.0.2"));
        Assert.Equal(2, buf.Count);
    }

    [Fact]
    public void ClearDeviceFilter_AcceptsAllAfter()
    {
        var buf = new PassiveProxyBuffer();
        buf.SetDeviceFilter(["10.0.0.1"]);
        buf.ClearDeviceFilter();
        buf.Add(MakeCapture(clientIp: "10.0.0.99"));
        Assert.Equal(1, buf.Count);
    }

    [Fact]
    public void GetSummary_ReturnsCorrectCounts()
    {
        var buf = new PassiveProxyBuffer();
        buf.Add(MakeCapture(host: "a.com", path: "/foo"));
        buf.Add(MakeCapture(host: "a.com", path: "/foo"));
        buf.Add(MakeCapture(host: "b.com", path: "/bar"));

        var summary = buf.GetSummary();

        Assert.Equal(3, summary.TotalRequests);
        Assert.Equal(2, summary.TopHosts.Count);
        Assert.Equal("a.com", summary.TopEndpoints[0].Host); // most frequent first
        Assert.Equal(2, summary.TopEndpoints[0].Count);
    }

    [Fact]
    public void GetSummary_ExposesActiveDeviceFilter()
    {
        var buf = new PassiveProxyBuffer();
        buf.SetDeviceFilter(["192.168.1.1", "192.168.1.2"]);
        var summary = buf.GetSummary();
        Assert.Contains("192.168.1.1", summary.ActiveDeviceFilter);
        Assert.Contains("192.168.1.2", summary.ActiveDeviceFilter);
    }

    [Fact]
    public void DeviceFilter_MultipleIps_AcceptsAll()
    {
        var buf = new PassiveProxyBuffer();
        buf.SetDeviceFilter(["10.0.0.1", "10.0.0.2"]);

        buf.Add(MakeCapture(clientIp: "10.0.0.1"));
        buf.Add(MakeCapture(clientIp: "10.0.0.2"));
        buf.Add(MakeCapture(clientIp: "10.0.0.3")); // filtered out

        Assert.Equal(2, buf.Count);
    }
}
