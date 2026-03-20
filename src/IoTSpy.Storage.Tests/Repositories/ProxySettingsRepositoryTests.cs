using IoTSpy.Core.Enums;
using IoTSpy.Core.Models;
using IoTSpy.Storage.Repositories;
using Xunit;

namespace IoTSpy.Storage.Tests.Repositories;

public class ProxySettingsRepositoryTests : IDisposable
{
    private readonly IoTSpyDbContext _db = TestDbContextFactory.Create();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetAsync_WhenNoSettings_ReturnsDefaults()
    {
        var repo = new ProxySettingsRepository(_db);
        var settings = await repo.GetAsync();

        Assert.NotNull(settings);
        // Default port is 8888
        Assert.Equal(8888, settings.ProxyPort);
    }

    [Fact]
    public async Task GetAsync_CalledTwice_ReturnsSameRow()
    {
        var repo = new ProxySettingsRepository(_db);
        var s1 = await repo.GetAsync();
        var s2 = await repo.GetAsync();

        Assert.Equal(s1.Id, s2.Id);
    }

    [Fact]
    public async Task SaveAsync_PersistsChanges()
    {
        var repo = new ProxySettingsRepository(_db);
        var settings = await repo.GetAsync();
        settings.ProxyPort = 9999;
        settings.Mode = ProxyMode.GatewayRedirect;

        await repo.SaveAsync(settings);

        var reloaded = await repo.GetAsync();
        Assert.Equal(9999, reloaded.ProxyPort);
        Assert.Equal(ProxyMode.GatewayRedirect, reloaded.Mode);
    }

    [Fact]
    public async Task SaveAsync_WhenNoExisting_CreatesRow()
    {
        var repo = new ProxySettingsRepository(_db);
        var newSettings = new ProxySettings { ProxyPort = 7777 };

        await repo.SaveAsync(newSettings);

        var reloaded = await repo.GetAsync();
        Assert.Equal(7777, reloaded.ProxyPort);
    }
}
