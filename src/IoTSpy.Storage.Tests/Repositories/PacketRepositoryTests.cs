using IoTSpy.Core.Models;
using IoTSpy.Storage.Repositories;
using Xunit;

namespace IoTSpy.Storage.Tests.Repositories;

public class PacketRepositoryTests : IDisposable
{
    private readonly IoTSpyDbContext _db = TestDbContextFactory.Create();
    private readonly Guid _deviceId;

    public PacketRepositoryTests()
    {
        // Insert a CaptureDevice so the FK constraint on CapturedPacket.DeviceId is satisfied.
        var device = new CaptureDevice { Id = Guid.NewGuid(), Name = "test0", DisplayName = "Test" };
        _db.CaptureDevices.Add(device);
        _db.SaveChanges();
        _deviceId = device.Id;
    }

    public void Dispose() => _db.Dispose();

    private CapturedPacket MakePacket(long captureIndex = 1, string protocol = "TCP") =>
        new()
        {
            Id = Guid.NewGuid(),
            DeviceId = _deviceId,
            CaptureIndex = captureIndex,
            Timestamp = DateTimeOffset.UtcNow,
            Protocol = protocol,
            Layer2Protocol = "Ethernet",
            Layer3Protocol = "IPv4",
            Layer4Protocol = "TCP",
            SourceIp = "10.0.0.1",
            DestinationIp = "10.0.0.2",
            SourceMac = string.Empty,
            DestinationMac = string.Empty,
            PayloadPreview = string.Empty,
        };

    [Fact]
    public async Task AddRangeAsync_PersistsAllPackets()
    {
        var repo = new CaptureDeviceRepository(_db);
        var packets = new[] { MakePacket(1), MakePacket(2), MakePacket(3) };

        await repo.AddRangeAsync(packets, TestContext.Current.CancellationToken);

        Assert.Equal(3, _db.Packets.Count());
    }

    [Fact]
    public async Task GetMaxCaptureIndexAsync_ReturnsZero_WhenEmpty()
    {
        var repo = new CaptureDeviceRepository(_db);

        var max = await repo.GetMaxCaptureIndexAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0L, max);
    }

    [Fact]
    public async Task GetMaxCaptureIndexAsync_ReturnsMaxIndex()
    {
        var repo = new CaptureDeviceRepository(_db);
        await repo.AddRangeAsync(new[] { MakePacket(5), MakePacket(12), MakePacket(3) }, TestContext.Current.CancellationToken);

        var max = await repo.GetMaxCaptureIndexAsync(TestContext.Current.CancellationToken);

        Assert.Equal(12L, max);
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsUpToLimit_OrderedByIndexAscending()
    {
        var repo = new CaptureDeviceRepository(_db);
        await repo.AddRangeAsync(new[] { MakePacket(1), MakePacket(2), MakePacket(3), MakePacket(4) }, TestContext.Current.CancellationToken);

        var result = await repo.GetRecentAsync(2, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Equal(3L, result[0].CaptureIndex);
        Assert.Equal(4L, result[1].CaptureIndex);
    }

    [Fact]
    public async Task DeleteAllAsync_RemovesEveryRow()
    {
        var repo = new CaptureDeviceRepository(_db);
        await repo.AddRangeAsync(new[] { MakePacket(1), MakePacket(2) }, TestContext.Current.CancellationToken);

        await repo.DeleteAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, _db.Packets.Count());
    }
}
