using IoTSpy.Core.Enums;
using IoTSpy.Core.Models;
using IoTSpy.Storage.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IoTSpy.Storage.Tests.Repositories;

public class ScanJobRepositoryFilterTests : IDisposable
{
    private readonly IoTSpyDbContext _db;

    public ScanJobRepositoryFilterTests()
    {
        var options = new DbContextOptionsBuilder<IoTSpyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new IoTSpyDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Guid Dev1, Guid Dev2)> SeedAsync(CancellationToken ct)
    {
        var dev1 = new Device { IpAddress = "10.0.0.1", MacAddress = "AA:BB:CC:DD:EE:01" };
        var dev2 = new Device { IpAddress = "10.0.0.2", MacAddress = "AA:BB:CC:DD:EE:02" };
        var now = DateTimeOffset.UtcNow;

        _db.Devices.AddRange(dev1, dev2);
        _db.ScanJobs.AddRange(
            new ScanJob { DeviceId = dev1.Id, TargetIp = "10.0.0.1", Status = ScanStatus.Completed, CreatedAt = now },
            new ScanJob { DeviceId = dev1.Id, TargetIp = "10.0.0.1", Status = ScanStatus.Completed, CreatedAt = now },
            new ScanJob { DeviceId = dev1.Id, TargetIp = "10.0.0.1", Status = ScanStatus.Failed,    CreatedAt = now },
            new ScanJob { DeviceId = dev2.Id, TargetIp = "10.0.0.2", Status = ScanStatus.Running,   CreatedAt = now.AddDays(-2) }
        );
        await _db.SaveChangesAsync(ct);
        return (dev1.Id, dev2.Id);
    }

    [Fact]
    public async Task GetAllAsync_FilterByStatus_ReturnsMatchingJobs()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedAsync(ct);
        var repo = new ScanJobRepository(_db);

        var result = await repo.GetAllAsync(status: ScanStatus.Completed, ct: ct);
        var count = await repo.CountAsync(status: ScanStatus.Completed, ct: ct);

        Assert.Equal(2, result.Count);
        Assert.Equal(2, count);
        Assert.All(result, j => Assert.Equal(ScanStatus.Completed, j.Status));
    }

    [Fact]
    public async Task GetAllAsync_FilterByDeviceId_ReturnsMatchingJobs()
    {
        var ct = TestContext.Current.CancellationToken;
        var (dev1, _) = await SeedAsync(ct);
        var repo = new ScanJobRepository(_db);

        var result = await repo.GetAllAsync(deviceId: dev1, ct: ct);
        var count = await repo.CountAsync(deviceId: dev1, ct: ct);

        Assert.Equal(3, result.Count);
        Assert.Equal(3, count);
        Assert.All(result, j => Assert.Equal(dev1, j.DeviceId));
    }

    [Fact]
    public async Task GetAllAsync_FilterByCreatedAfter_ReturnsNewerJobsOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedAsync(ct);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-1);
        var repo = new ScanJobRepository(_db);

        var result = await repo.GetAllAsync(createdAfter: cutoff, ct: ct);
        var count = await repo.CountAsync(createdAfter: cutoff, ct: ct);

        // 3 jobs have CreatedAt = now (after cutoff); 1 has CreatedAt = now - 2 days
        Assert.Equal(3, result.Count);
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task GetAllAsync_CombinedFilters_ApplyAll()
    {
        var ct = TestContext.Current.CancellationToken;
        var (dev1, _) = await SeedAsync(ct);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-1);
        var repo = new ScanJobRepository(_db);

        var result = await repo.GetAllAsync(status: ScanStatus.Completed, deviceId: dev1, createdAfter: cutoff, ct: ct);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllAsync_NoFilters_ReturnsAllJobs()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedAsync(ct);
        var repo = new ScanJobRepository(_db);

        var result = await repo.GetAllAsync(ct: ct);
        var count = await repo.CountAsync(ct: ct);

        Assert.Equal(4, result.Count);
        Assert.Equal(4, count);
    }
}
