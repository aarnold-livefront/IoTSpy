using System.Text.Json;
using System.Text.Json.Serialization;
using IoTSpy.Core.Models;
using IoTSpy.Storage.Redis;
using NSubstitute;
using StackExchange.Redis;
using Xunit;

namespace IoTSpy.Storage.Tests.Redis;

public class RedisPassiveProxyBufferTests
{
    private readonly IDatabase _db;
    private readonly IConnectionMultiplexer _mux;
    private readonly RedisOptions _options = new() { KeyPrefix = "test", PassiveBufferCapacity = 5 };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public RedisPassiveProxyBufferTests()
    {
        _db = Substitute.For<IDatabase>();
        _mux = Substitute.For<IConnectionMultiplexer>();
        _mux.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_db);

        // Default: empty filter on startup
        _db.SetMembers(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns([]);
    }

    private RedisPassiveProxyBuffer CreateBuffer() => new(_mux, _options);

    private static RedisValue Serialize(CapturedRequest r) =>
        JsonSerializer.Serialize(r, JsonOpts);

    private static CapturedRequest MakeCapture(
        string clientIp = "10.0.0.1",
        string host = "api.example.com",
        string path = "/data",
        int status = 200) =>
        new() { ClientIp = clientIp, Host = host, Path = path, Method = "GET", StatusCode = status };

    [Fact]
    public void Add_PushesJsonToRedis()
    {
        var buf = CreateBuffer();
        var capture = MakeCapture();

        buf.Add(capture);

        _db.Received(1).ListRightPush(
            Arg.Is<RedisKey>(k => k == "test:passive:buffer"),
            Arg.Any<RedisValue>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public void Add_TrimsAfterPush()
    {
        var buf = CreateBuffer();
        buf.Add(MakeCapture());

        _db.Received(1).ListTrim(
            Arg.Is<RedisKey>(k => k == "test:passive:buffer"),
            -_options.PassiveBufferCapacity,
            -1,
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public void Add_WithActiveFilter_SkipsNonMatchingCapture()
    {
        _db.SetMembers(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
           .Returns([(RedisValue)"10.0.0.1"]);

        var buf = CreateBuffer();
        buf.Add(MakeCapture(clientIp: "10.0.0.99")); // not in filter

        _db.DidNotReceive().ListRightPush(
            Arg.Any<RedisKey>(),
            Arg.Any<RedisValue>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public void Add_WithActiveFilter_AcceptsMatchingCapture()
    {
        _db.SetMembers(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
           .Returns([(RedisValue)"10.0.0.1"]);

        var buf = CreateBuffer();
        buf.Add(MakeCapture(clientIp: "10.0.0.1")); // in filter

        _db.Received(1).ListRightPush(
            Arg.Any<RedisKey>(),
            Arg.Any<RedisValue>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public void Count_ReturnsRedisListLength()
    {
        _db.ListLength(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(42L);
        var buf = CreateBuffer();
        Assert.Equal(42, buf.Count);
    }

    [Fact]
    public void Snapshot_DeserializesAllEntries()
    {
        var capture = MakeCapture(host: "example.com", path: "/test");
        _db.ListRange(
                Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CommandFlags>())
           .Returns([Serialize(capture)]);

        var buf = CreateBuffer();
        var result = buf.Snapshot();

        Assert.Single(result);
        Assert.Equal("example.com", result[0].Host);
        Assert.Equal("/test", result[0].Path);
    }

    [Fact]
    public void Snapshot_IgnoresMalformedEntries()
    {
        _db.ListRange(
                Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CommandFlags>())
           .Returns([(RedisValue)"not-json"]);

        var buf = CreateBuffer();
        // JsonSerializer.Deserialize returns null on failure; OfType<> filters nulls
        Assert.Empty(buf.Snapshot());
    }

    [Fact]
    public void Clear_DeletesBufferKey()
    {
        var buf = CreateBuffer();
        buf.Clear();

        _db.Received(1).KeyDelete(
            Arg.Is<RedisKey>(k => k == "test:passive:buffer"),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public void SetDeviceFilter_StoresIpsInRedis()
    {
        var buf = CreateBuffer();
        buf.SetDeviceFilter(["192.168.1.1", "192.168.1.2"]);

        _db.Received(1).KeyDelete(
            Arg.Is<RedisKey>(k => k == "test:passive:filter"),
            Arg.Any<CommandFlags>());
        _db.Received(1).SetAdd(
            Arg.Is<RedisKey>(k => k == "test:passive:filter"),
            Arg.Any<RedisValue[]>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public void SetDeviceFilter_UpdatesLocalCache_AffectsSubsequentAdds()
    {
        var buf = CreateBuffer();
        buf.SetDeviceFilter(["10.10.10.1"]);

        buf.Add(MakeCapture(clientIp: "10.10.10.99")); // not in filter
        _db.DidNotReceive().ListRightPush(
            Arg.Any<RedisKey>(),
            Arg.Any<RedisValue>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public void ClearDeviceFilter_DeletesFilterKey()
    {
        var buf = CreateBuffer();
        buf.ClearDeviceFilter();

        _db.Received().KeyDelete(
            Arg.Is<RedisKey>(k => k == "test:passive:filter"),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public void ClearDeviceFilter_AllowsAllSubsequentAdds()
    {
        _db.SetMembers(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
           .Returns([(RedisValue)"10.0.0.1"]);

        var buf = CreateBuffer();
        buf.ClearDeviceFilter();
        buf.Add(MakeCapture(clientIp: "10.0.0.99")); // previously filtered

        _db.Received(1).ListRightPush(
            Arg.Any<RedisKey>(),
            Arg.Any<RedisValue>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public void DeviceFilter_LoadedFromRedisOnConstruction()
    {
        _db.SetMembers(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
           .Returns([(RedisValue)"192.168.1.1"]);

        var buf = CreateBuffer();

        Assert.Contains("192.168.1.1", buf.DeviceFilter);
    }

    [Fact]
    public void GetSummary_ReturnsCorrectCounts()
    {
        var captures = new[]
        {
            MakeCapture(host: "a.com", path: "/foo", status: 200),
            MakeCapture(host: "a.com", path: "/foo", status: 200),
            MakeCapture(host: "b.com", path: "/bar", status: 404)
        };
        _db.ListRange(
                Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CommandFlags>())
           .Returns(captures.Select(Serialize).ToArray());

        var buf = CreateBuffer();
        var summary = buf.GetSummary();

        Assert.Equal(3, summary.TotalRequests);
        Assert.Equal(2, summary.TopHosts.Count);
        Assert.Equal("a.com", summary.TopEndpoints[0].Host);
        Assert.Equal(2, summary.TopEndpoints[0].Count);
        Assert.Contains(summary.StatusCodes, b => b.StatusCode == 200 && b.Count == 2);
        Assert.Contains(summary.StatusCodes, b => b.StatusCode == 404 && b.Count == 1);
    }

    [Fact]
    public void GetSummary_ExposesActiveDeviceFilter()
    {
        _db.SetMembers(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
           .Returns([(RedisValue)"10.0.0.1", (RedisValue)"10.0.0.2"]);
        _db.ListRange(
                Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CommandFlags>())
           .Returns([]);

        var buf = CreateBuffer();
        var summary = buf.GetSummary();

        Assert.Contains("10.0.0.1", summary.ActiveDeviceFilter);
        Assert.Contains("10.0.0.2", summary.ActiveDeviceFilter);
    }
}
