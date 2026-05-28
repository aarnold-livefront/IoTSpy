using IoTSpy.Core.Models;
using IoTSpy.Storage.Repositories;
using Xunit;

namespace IoTSpy.Analytics.Tests.Repositories;

public class TrafficInsightRepositoryTests : IDisposable
{
    private readonly IoTSpy.Storage.IoTSpyDbContext _db;
    private readonly TrafficInsightRepository _sut;

    public TrafficInsightRepositoryTests()
    {
        _db = TestDbContextFactory.Create();
        _sut = new TrafficInsightRepository(_db);

        // Seed a capture so FK constraints are satisfied
        _db.Captures.Add(new CapturedRequest
        {
            Id = SeedCaptureId,
            Method = "GET",
            Host = "example.com",
            Protocol = IoTSpy.Core.Enums.InterceptionProtocol.Https,
            Timestamp = DateTimeOffset.UtcNow
        });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private static readonly Guid SeedCaptureId = Guid.NewGuid();

    private static TrafficInsight MakeInsight(Guid? captureId = null) => new()
    {
        Id = Guid.NewGuid(),
        CaptureId = captureId ?? SeedCaptureId,
        TagsJson = """["DataBroker"]""",
        ConfidenceJson = """{"DataBroker":0.9}""",
        RiskScore = 0.5,
        ModelVersion = "rule-v1",
        Source = "rule",
        CreatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task UpsertAsync_NewInsight_CreatesRecord()
    {
        var insight = MakeInsight();

        await _sut.UpsertAsync(insight, TestContext.Current.CancellationToken);

        var stored = await _sut.GetByCaptureIdAsync(SeedCaptureId, TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.Equal(0.5, stored!.RiskScore);
    }

    [Fact]
    public async Task UpsertAsync_ExistingInsight_UpdatesWithoutDuplicate()
    {
        await _sut.UpsertAsync(MakeInsight(), TestContext.Current.CancellationToken);

        var updated = MakeInsight();
        updated.RiskScore = 0.9;
        updated.TagsJson = """["DataBroker","PiiDetected"]""";

        await _sut.UpsertAsync(updated, TestContext.Current.CancellationToken);

        var count = _db.TrafficInsights.Count(i => i.CaptureId == SeedCaptureId);
        Assert.Equal(1, count);

        var stored = await _sut.GetByCaptureIdAsync(SeedCaptureId, TestContext.Current.CancellationToken);
        Assert.Equal(0.9, stored!.RiskScore);
        Assert.Contains("PiiDetected", stored.TagsJson);
    }

    [Fact]
    public async Task GetByCaptureIdAsync_NotFound_ReturnsNull()
    {
        var result = await _sut.GetByCaptureIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetTriageQueueAsync_OrdersByRiskScoreDescending()
    {
        // Seed three captures with different risk scores
        var captures = new[]
        {
            new CapturedRequest { Id = Guid.NewGuid(), Method = "GET", Host = "a.com", Protocol = IoTSpy.Core.Enums.InterceptionProtocol.Https, Timestamp = DateTimeOffset.UtcNow },
            new CapturedRequest { Id = Guid.NewGuid(), Method = "GET", Host = "b.com", Protocol = IoTSpy.Core.Enums.InterceptionProtocol.Https, Timestamp = DateTimeOffset.UtcNow },
            new CapturedRequest { Id = Guid.NewGuid(), Method = "GET", Host = "c.com", Protocol = IoTSpy.Core.Enums.InterceptionProtocol.Https, Timestamp = DateTimeOffset.UtcNow }
        };
        _db.Captures.AddRange(captures);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var scores = new[] { 0.3, 0.9, 0.6 };
        for (var i = 0; i < captures.Length; i++)
        {
            var insight = MakeInsight(captures[i].Id);
            insight.RiskScore = scores[i];
            await _sut.UpsertAsync(insight, TestContext.Current.CancellationToken);
        }

        var queue = await _sut.GetTriageQueueAsync(1, 10, unreviewedOnly: false, TestContext.Current.CancellationToken);

        // Verify descending order
        for (var i = 0; i < queue.Count - 1; i++)
            Assert.True(queue[i].RiskScore >= queue[i + 1].RiskScore);
    }

    [Fact]
    public async Task GetTriageQueueAsync_UnreviewedOnly_ExcludesReviewed()
    {
        var reviewed = MakeInsight();
        reviewed.IsReviewed = true;
        var unreviewed = new CapturedRequest { Id = Guid.NewGuid(), Method = "GET", Host = "x.com", Protocol = IoTSpy.Core.Enums.InterceptionProtocol.Https, Timestamp = DateTimeOffset.UtcNow };
        _db.Captures.Add(unreviewed);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var unreviewedInsight = MakeInsight(unreviewed.Id);

        await _sut.UpsertAsync(reviewed, TestContext.Current.CancellationToken);
        await _sut.UpsertAsync(unreviewedInsight, TestContext.Current.CancellationToken);

        var queue = await _sut.GetTriageQueueAsync(1, 10, unreviewedOnly: true, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(queue, i => i.CaptureId == SeedCaptureId);
        Assert.Contains(queue, i => i.CaptureId == unreviewed.Id);
    }

    [Fact]
    public async Task MarkReviewedAsync_SetsReviewFields()
    {
        await _sut.UpsertAsync(MakeInsight(), TestContext.Current.CancellationToken);
        var stored = await _sut.GetByCaptureIdAsync(SeedCaptureId, TestContext.Current.CancellationToken);
        var userId = Guid.NewGuid();

        await _sut.MarkReviewedAsync(stored!.Id, userId, dismissed: false, "Confirmed threat",
            TestContext.Current.CancellationToken);

        var updated = await _sut.GetByCaptureIdAsync(SeedCaptureId, TestContext.Current.CancellationToken);
        Assert.True(updated!.IsReviewed);
        Assert.False(updated.IsDismissed);
        Assert.Equal("Confirmed threat", updated.ReviewNote);
        Assert.Equal(userId, updated.ReviewedByUserId);
        Assert.NotNull(updated.ReviewedAt);
    }

    [Fact]
    public async Task GetByCaptureIdsAsync_ReturnsMappedInsights()
    {
        var extra = new CapturedRequest { Id = Guid.NewGuid(), Method = "GET", Host = "y.com", Protocol = IoTSpy.Core.Enums.InterceptionProtocol.Https, Timestamp = DateTimeOffset.UtcNow };
        _db.Captures.Add(extra);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _sut.UpsertAsync(MakeInsight(SeedCaptureId), TestContext.Current.CancellationToken);
        await _sut.UpsertAsync(MakeInsight(extra.Id), TestContext.Current.CancellationToken);

        var result = await _sut.GetByCaptureIdsAsync(
            [SeedCaptureId, extra.Id, Guid.NewGuid()],
            TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, i => i.CaptureId == SeedCaptureId);
        Assert.Contains(result, i => i.CaptureId == extra.Id);
    }
}
