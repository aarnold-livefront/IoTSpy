using IoTSpy.Core.Enums;
using IoTSpy.Core.Models;
using IoTSpy.Storage.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IoTSpy.Storage.Tests.Repositories;

public class FuzzerJobRepositoryFilterTests : IDisposable
{
    private readonly IoTSpyDbContext _db;

    public FuzzerJobRepositoryFilterTests()
    {
        var options = new DbContextOptionsBuilder<IoTSpyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new IoTSpyDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Guid Cap1, Guid Cap2)> SeedAsync(CancellationToken ct)
    {
        var cap1 = new CapturedRequest { Host = "a.test", Method = "GET", Path = "/", Timestamp = DateTimeOffset.UtcNow };
        var cap2 = new CapturedRequest { Host = "b.test", Method = "POST", Path = "/api", Timestamp = DateTimeOffset.UtcNow };

        _db.Captures.AddRange(cap1, cap2);
        _db.FuzzerJobs.AddRange(
            new FuzzerJob { BaseCaptureId = cap1.Id, Strategy = FuzzerStrategy.Random, Status = FuzzerJobStatus.Completed },
            new FuzzerJob { BaseCaptureId = cap1.Id, Strategy = FuzzerStrategy.Random, Status = FuzzerJobStatus.Completed },
            new FuzzerJob { BaseCaptureId = cap1.Id, Strategy = FuzzerStrategy.Random, Status = FuzzerJobStatus.Failed },
            new FuzzerJob { BaseCaptureId = cap2.Id, Strategy = FuzzerStrategy.Random, Status = FuzzerJobStatus.Running }
        );
        await _db.SaveChangesAsync(ct);
        return (cap1.Id, cap2.Id);
    }

    [Fact]
    public async Task GetAllAsync_FilterByStatus_ReturnsMatchingJobs()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedAsync(ct);
        var repo = new FuzzerJobRepository(_db);

        var result = await repo.GetAllAsync(status: FuzzerJobStatus.Completed, ct: ct);
        var count = await repo.CountAsync(status: FuzzerJobStatus.Completed, ct: ct);

        Assert.Equal(2, result.Count);
        Assert.Equal(2, count);
        Assert.All(result, j => Assert.Equal(FuzzerJobStatus.Completed, j.Status));
    }

    [Fact]
    public async Task GetAllAsync_FilterByCaptureId_ReturnsMatchingJobs()
    {
        var ct = TestContext.Current.CancellationToken;
        var (cap1, _) = await SeedAsync(ct);
        var repo = new FuzzerJobRepository(_db);

        var result = await repo.GetAllAsync(captureId: cap1, ct: ct);
        var count = await repo.CountAsync(captureId: cap1, ct: ct);

        Assert.Equal(3, result.Count);
        Assert.Equal(3, count);
        Assert.All(result, j => Assert.Equal(cap1, j.BaseCaptureId));
    }

    [Fact]
    public async Task GetAllAsync_CombinedStatusAndCapture_ApplyBoth()
    {
        var ct = TestContext.Current.CancellationToken;
        var (cap1, _) = await SeedAsync(ct);
        var repo = new FuzzerJobRepository(_db);

        var result = await repo.GetAllAsync(status: FuzzerJobStatus.Completed, captureId: cap1, ct: ct);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllAsync_NoFilters_ReturnsAllJobs()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedAsync(ct);
        var repo = new FuzzerJobRepository(_db);

        var result = await repo.GetAllAsync(ct: ct);
        var count = await repo.CountAsync(ct: ct);

        Assert.Equal(4, result.Count);
        Assert.Equal(4, count);
    }
}
