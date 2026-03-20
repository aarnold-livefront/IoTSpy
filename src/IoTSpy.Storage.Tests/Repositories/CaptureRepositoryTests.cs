using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using IoTSpy.Storage.Repositories;
using Xunit;

namespace IoTSpy.Storage.Tests.Repositories;

public class CaptureRepositoryTests : IDisposable
{
    private readonly IoTSpyDbContext _db = TestDbContextFactory.Create();

    public void Dispose() => _db.Dispose();

    private static CapturedRequest MakeCapture(string host = "example.com", string method = "GET", int status = 200) =>
        new()
        {
            Host = host,
            Method = method,
            Path = "/test",
            StatusCode = status,
            Timestamp = DateTimeOffset.UtcNow
        };

    [Fact]
    public async Task AddAsync_AddsCapture()
    {
        var repo = new CaptureRepository(_db);
        var capture = MakeCapture();

        var result = await repo.AddAsync(capture);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(1, await repo.CountAsync(new CaptureFilter()));
    }

    [Fact]
    public async Task GetByIdAsync_WhenFound_ReturnsCapture()
    {
        var repo = new CaptureRepository(_db);
        var capture = await repo.AddAsync(MakeCapture());

        var result = await repo.GetByIdAsync(capture.Id);

        Assert.NotNull(result);
        Assert.Equal(capture.Id, result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        var repo = new CaptureRepository(_db);
        Assert.Null(await repo.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsCorrectPage()
    {
        var repo = new CaptureRepository(_db);
        for (int i = 0; i < 5; i++)
            await repo.AddAsync(MakeCapture($"host{i}.com"));

        var page1 = await repo.GetPagedAsync(new CaptureFilter(), 1, 3);
        var page2 = await repo.GetPagedAsync(new CaptureFilter(), 2, 3);

        Assert.Equal(3, page1.Count);
        Assert.Equal(2, page2.Count);
    }

    [Fact]
    public async Task CountAsync_ReturnsCorrectTotal()
    {
        var repo = new CaptureRepository(_db);
        await repo.AddAsync(MakeCapture());
        await repo.AddAsync(MakeCapture());

        Assert.Equal(2, await repo.CountAsync(new CaptureFilter()));
    }

    [Fact]
    public async Task GetPagedAsync_FilterByHost_ReturnsFiltered()
    {
        var repo = new CaptureRepository(_db);
        await repo.AddAsync(MakeCapture("api.example.com"));
        await repo.AddAsync(MakeCapture("other.com"));

        var filter = new CaptureFilter(HostContains: "example");
        var results = await repo.GetPagedAsync(filter, 1, 50);

        Assert.Single(results);
        Assert.Equal("api.example.com", results[0].Host);
    }

    [Fact]
    public async Task GetPagedAsync_FilterByStatusCode_ReturnsFiltered()
    {
        var repo = new CaptureRepository(_db);
        await repo.AddAsync(MakeCapture(status: 200));
        await repo.AddAsync(MakeCapture(status: 404));
        await repo.AddAsync(MakeCapture(status: 200));

        var filter = new CaptureFilter(StatusCode: 404);
        var results = await repo.GetPagedAsync(filter, 1, 50);

        Assert.Single(results);
        Assert.Equal(404, results[0].StatusCode);
    }

    [Fact]
    public async Task DeleteAsync_RemovesCapture()
    {
        var repo = new CaptureRepository(_db);
        var capture = await repo.AddAsync(MakeCapture());

        await repo.DeleteAsync(capture.Id);

        Assert.Null(await repo.GetByIdAsync(capture.Id));
    }

    [Fact]
    public async Task ClearAsync_WithNoFilter_RemovesAll()
    {
        var repo = new CaptureRepository(_db);
        await repo.AddAsync(MakeCapture());
        await repo.AddAsync(MakeCapture());

        await repo.ClearAsync();

        Assert.Equal(0, await repo.CountAsync(new CaptureFilter()));
    }

    [Fact]
    public async Task ClearAsync_WithDeviceId_OnlyClearsMatchingDevice()
    {
        var repo = new CaptureRepository(_db);
        var deviceId = Guid.NewGuid();

        var c1 = MakeCapture(); c1.DeviceId = deviceId;
        var c2 = MakeCapture(); c2.DeviceId = deviceId;
        await repo.AddAsync(c1);
        await repo.AddAsync(c2);
        await repo.AddAsync(MakeCapture());  // No device

        await repo.ClearAsync(deviceId);

        Assert.Equal(1, await repo.CountAsync(new CaptureFilter()));
    }
}
