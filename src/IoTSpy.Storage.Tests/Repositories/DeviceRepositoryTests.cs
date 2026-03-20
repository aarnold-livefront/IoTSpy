using IoTSpy.Core.Models;
using IoTSpy.Storage.Repositories;
using Xunit;

namespace IoTSpy.Storage.Tests.Repositories;

public class DeviceRepositoryTests : IDisposable
{
    private readonly IoTSpyDbContext _db = TestDbContextFactory.Create();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetAllAsync_WhenEmpty_ReturnsEmptyList()
    {
        var repo = new DeviceRepository(_db);
        var result = await repo.GetAllAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task UpsertByIpAsync_NewDevice_AddsDevice()
    {
        var repo = new DeviceRepository(_db);
        var device = new Device { IpAddress = "192.168.1.10", Label = "Test" };

        var result = await repo.UpsertByIpAsync(device);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("192.168.1.10", result.IpAddress);

        var all = await repo.GetAllAsync();
        Assert.Single(all);
    }

    [Fact]
    public async Task UpsertByIpAsync_ExistingIp_UpdatesLastSeen()
    {
        var repo = new DeviceRepository(_db);
        var device = new Device { IpAddress = "192.168.1.20", Label = "First" };
        await repo.UpsertByIpAsync(device);

        var updated = new Device { IpAddress = "192.168.1.20", Label = "Second" };
        await repo.UpsertByIpAsync(updated);

        var all = await repo.GetAllAsync();
        Assert.Single(all);
    }

    [Fact]
    public async Task GetByIdAsync_WhenFound_ReturnsDevice()
    {
        var repo = new DeviceRepository(_db);
        var inserted = await repo.UpsertByIpAsync(new Device { IpAddress = "10.0.0.1" });

        var result = await repo.GetByIdAsync(inserted.Id);

        Assert.NotNull(result);
        Assert.Equal("10.0.0.1", result.IpAddress);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        var repo = new DeviceRepository(_db);
        var result = await repo.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIpAsync_WhenFound_ReturnsDevice()
    {
        var repo = new DeviceRepository(_db);
        await repo.UpsertByIpAsync(new Device { IpAddress = "10.0.0.2" });

        var result = await repo.GetByIpAsync("10.0.0.2");

        Assert.NotNull(result);
        Assert.Equal("10.0.0.2", result.IpAddress);
    }

    [Fact]
    public async Task GetByIpAsync_WhenNotFound_ReturnsNull()
    {
        var repo = new DeviceRepository(_db);
        var result = await repo.GetByIpAsync("99.99.99.99");
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var repo = new DeviceRepository(_db);
        var device = await repo.UpsertByIpAsync(new Device { IpAddress = "10.0.0.3", Label = "Old" });

        device.Label = "Updated";
        await repo.UpdateAsync(device);

        var refetched = await repo.GetByIdAsync(device.Id);
        Assert.Equal("Updated", refetched!.Label);
    }

    [Fact]
    public async Task DeleteAsync_RemovesDevice()
    {
        var repo = new DeviceRepository(_db);
        var device = await repo.UpsertByIpAsync(new Device { IpAddress = "10.0.0.4" });

        await repo.DeleteAsync(device.Id);

        var result = await repo.GetByIdAsync(device.Id);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsMultipleDevices()
    {
        var repo = new DeviceRepository(_db);
        await repo.UpsertByIpAsync(new Device { IpAddress = "10.0.1.1" });
        await repo.UpsertByIpAsync(new Device { IpAddress = "10.0.1.2" });
        await repo.UpsertByIpAsync(new Device { IpAddress = "10.0.1.3" });

        var all = await repo.GetAllAsync();

        Assert.Equal(3, all.Count);
    }
}
