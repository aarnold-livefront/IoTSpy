using System.Text.Json;
using System.Text.Json.Serialization;
using IoTSpy.Core.Models;
using IoTSpy.Storage.Redis;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackExchange.Redis;
using Xunit;

namespace IoTSpy.Storage.Tests.Redis;

public class RedisPassiveProxyBufferTests
{
    private readonly IDatabase _db;
    private readonly IConnectionMultiplexer _mux;
    private readonly IBatch _batch;
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

        // Default: no filter on startup
        _db.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
           .Returns(Task.FromResult(Array.Empty<RedisValue>()));

        // Batch mock used by PersistBatch
        _batch = Substitute.For<IBatch>();
        _batch.ListRightPushAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
              .Returns(Task.FromResult(1L));
        _db.CreateBatch().Returns(_batch);

        // Needed by SetDeviceFilter atomic rename
        _db.KeyRename(Arg.Any<RedisKey>(), Arg.Any<RedisKey>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
           .Returns(true);
    }

    private RedisPassiveProxyBuffer CreateBuffer() =>
        new(_mux, _options, NullLogger<RedisPassiveProxyBuffer>.Instance);

    private static RedisValue Serialize(CapturedRequest r) =>
        JsonSerializer.Serialize(r, JsonOpts);

    private static CapturedRequest MakeCapture(
        string clientIp = "10.0.0.1",
        string host = "api.example.com",
        string path = "/data",
        int status = 200) =>
        new() { ClientIp = clientIp, Host = host, Path = path, Method = "GET", StatusCode = status };

    // ── Add (async — writes via Channel; background consumer calls Redis) ────

    [Fact]
    public async Task Add_PushesJsonToRedis()
    {
        var buf = CreateBuffer();
        await buf.StartAsync(CancellationToken.None);

        buf.Add(MakeCapture());

        await Task.Delay(250, CancellationToken.None);
        await buf.StopAsync(CancellationToken.None);

        await _batch.Received().ListRightPushAsync(
            Arg.Is<RedisKey>(k => k == "test:passive:buffer"),
            Arg.Any<RedisValue>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task Add_TrimsAfterPush()
    {
        var buf = CreateBuffer();
        await buf.StartAsync(CancellationToken.None);

        buf.Add(MakeCapture());

        await Task.Delay(250, CancellationToken.None);
        await buf.StopAsync(CancellationToken.None);

        await _batch.Received().ListTrimAsync(
            Arg.Is<RedisKey>(k => k == "test:passive:buffer"),
            Arg.Is<long>(v => v == -_options.PassiveBufferCapacity),
            Arg.Is<long>(v => v == -1),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task Add_WithActiveFilter_SkipsNonMatchingCapture()
    {
        var buf = CreateBuffer();
        await buf.StartAsync(CancellationToken.None);
        buf.SetDeviceFilter(["10.0.0.1"]);

        buf.Add(MakeCapture(clientIp: "10.0.0.99")); // not in filter

        await Task.Delay(250, CancellationToken.None);
        await buf.StopAsync(CancellationToken.None);

        await _batch.DidNotReceive().ListRightPushAsync(
            Arg.Any<RedisKey>(),
            Arg.Any<RedisValue>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task Add_WithActiveFilter_AcceptsMatchingCapture()
    {
        var buf = CreateBuffer();
        await buf.StartAsync(CancellationToken.None);
        buf.SetDeviceFilter(["10.0.0.1"]);

        buf.Add(MakeCapture(clientIp: "10.0.0.1")); // in filter

        await Task.Delay(250, CancellationToken.None);
        await buf.StopAsync(CancellationToken.None);

        await _batch.Received().ListRightPushAsync(
            Arg.Any<RedisKey>(),
            Arg.Any<RedisValue>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>());
    }

    // ── Filter loading ────────────────────────────────────────────────────────

    [Fact]
    public async Task DeviceFilter_LoadedFromRedisOnStart()
    {
        _db.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
           .Returns(Task.FromResult<RedisValue[]>([(RedisValue)"192.168.1.1"]));

        var buf = CreateBuffer();
        await buf.StartAsync(CancellationToken.None);

        Assert.Contains("192.168.1.1", buf.DeviceFilter);

        await buf.StopAsync(CancellationToken.None);
    }

    // ── Synchronous read paths (Count, Snapshot, Clear) ──────────────────────

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

    // ── SetDeviceFilter ───────────────────────────────────────────────────────

    [Fact]
    public void SetDeviceFilter_StoresIpsInRedis()
    {
        // SetDeviceFilter uses an atomic temp-key rename: writes to "{filter}:tmp",
        // then renames it to the real filter key — no empty-filter window for other instances.
        var buf = CreateBuffer();
        buf.SetDeviceFilter(["192.168.1.1", "192.168.1.2"]);

        _db.Received(1).KeyDelete(
            Arg.Is<RedisKey>(k => k.ToString().EndsWith(":tmp")),
            Arg.Any<CommandFlags>());
        _db.Received(1).SetAdd(
            Arg.Is<RedisKey>(k => k.ToString().EndsWith(":tmp")),
            Arg.Any<RedisValue[]>(),
            Arg.Any<CommandFlags>());
        _db.Received(1).KeyRename(
            Arg.Is<RedisKey>(k => k.ToString().EndsWith(":tmp")),
            Arg.Is<RedisKey>(k => k == "test:passive:filter"),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task SetDeviceFilter_UpdatesLocalCache_AffectsSubsequentAdds()
    {
        var buf = CreateBuffer();
        await buf.StartAsync(CancellationToken.None);
        buf.SetDeviceFilter(["10.10.10.1"]);

        buf.Add(MakeCapture(clientIp: "10.10.10.99")); // not in filter

        await Task.Delay(250, CancellationToken.None);
        await buf.StopAsync(CancellationToken.None);

        await _batch.DidNotReceive().ListRightPushAsync(
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
    public async Task ClearDeviceFilter_AllowsAllSubsequentAdds()
    {
        var buf = CreateBuffer();
        await buf.StartAsync(CancellationToken.None);
        buf.SetDeviceFilter(["10.0.0.1"]);
        buf.ClearDeviceFilter();

        buf.Add(MakeCapture(clientIp: "10.0.0.99")); // previously filtered, now allowed

        await Task.Delay(250, CancellationToken.None);
        await buf.StopAsync(CancellationToken.None);

        await _batch.Received().ListRightPushAsync(
            Arg.Any<RedisKey>(),
            Arg.Any<RedisValue>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>());
    }

    // ── GetSummary ────────────────────────────────────────────────────────────

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
        _db.ListRange(
                Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CommandFlags>())
           .Returns([]);

        var buf = CreateBuffer();
        buf.SetDeviceFilter(["10.0.0.1", "10.0.0.2"]);
        var summary = buf.GetSummary();

        Assert.Contains("10.0.0.1", summary.ActiveDeviceFilter);
        Assert.Contains("10.0.0.2", summary.ActiveDeviceFilter);
    }
}
