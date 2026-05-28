using IoTSpy.Analytics.Rules;
using IoTSpy.Analytics.Services;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using IoTSpy.Storage.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace IoTSpy.Analytics.Tests.Services;

public class InsightServiceTests : IDisposable
{
    private readonly IoTSpy.Storage.IoTSpyDbContext _db;
    private readonly ITrafficInsightRepository _insightRepo;
    private readonly ICaptureRepository _captureRepo;
    private readonly RuleBasedTagger _tagger;
    private readonly InsightService _sut;

    public InsightServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _insightRepo = new TrafficInsightRepository(_db);
        _captureRepo = new CaptureRepository(_db);
        _tagger = new RuleBasedTagger();
        _sut = new InsightService(_tagger, _insightRepo, _captureRepo,
            NullLogger<InsightService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static CapturedRequest MakeCapture(Action<CapturedRequest>? configure = null)
    {
        var c = new CapturedRequest
        {
            Id = Guid.NewGuid(),
            Host = "api.example.com",
            Port = 443,
            StatusCode = 200,
            IsTls = true,
            TlsVersion = "TLSv1.3",
            Protocol = InterceptionProtocol.Https,
            Timestamp = DateTimeOffset.UtcNow,
            Method = "GET"
        };
        configure?.Invoke(c);
        return c;
    }

    private async Task SaveCapture(CapturedRequest capture)
    {
        _db.Captures.Add(capture);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ScoreAsync_CleanCapture_CreatesInsightWithZeroRisk()
    {
        var capture = MakeCapture();
        await SaveCapture(capture);

        var insight = await _sut.ScoreAsync(capture, TestContext.Current.CancellationToken);

        Assert.Equal(capture.Id, insight.CaptureId);
        Assert.Equal(0.0, insight.RiskScore);
        Assert.Equal("rule-v1", insight.ModelVersion);

        var tags = JsonSerializer.Deserialize<string[]>(insight.TagsJson);
        Assert.Empty(tags!);
    }

    [Fact]
    public async Task ScoreAsync_FlaggedCapture_PersistsInsightToDb()
    {
        var capture = MakeCapture(x =>
        {
            x.Host = "doubleclick.net";
            x.RequestBody = """{"email":"user@example.com"}""";
        });
        await SaveCapture(capture);

        var insight = await _sut.ScoreAsync(capture, TestContext.Current.CancellationToken);

        Assert.True(insight.RiskScore > 0);

        var stored = await _insightRepo.GetByCaptureIdAsync(capture.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.Equal(insight.RiskScore, stored!.RiskScore);
    }

    [Fact]
    public async Task ScoreAsync_CalledTwiceForSameCaptureId_IsIdempotent()
    {
        var capture = MakeCapture(x => x.Port = 9999);
        await SaveCapture(capture);

        await _sut.ScoreAsync(capture, TestContext.Current.CancellationToken);
        var second = await _sut.ScoreAsync(capture, TestContext.Current.CancellationToken);

        var count = _db.TrafficInsights.Count(i => i.CaptureId == capture.Id);
        Assert.Equal(1, count);
        Assert.True(second.RiskScore > 0);
    }

    [Fact]
    public async Task ScoreByCaptureIdAsync_CaptureNotInDb_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.ScoreByCaptureIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ScoreByCaptureIdAsync_CaptureExists_ReturnsInsight()
    {
        var capture = MakeCapture(x => x.Host = "amplitude.com");
        _db.Captures.Add(capture);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var insight = await _sut.ScoreByCaptureIdAsync(capture.Id, TestContext.Current.CancellationToken);

        Assert.Equal(capture.Id, insight.CaptureId);
        Assert.True(insight.RiskScore > 0);
    }

    [Fact]
    public async Task ScoreAsync_ExfiltrationRisk_HasHigherRiskScore()
    {
        var risky = MakeCapture(x =>
        {
            x.Host = "external.io";
            x.ResponseBodySize = 2_000_000;
        });
        var normal = MakeCapture();
        await SaveCapture(risky);
        await SaveCapture(normal);

        var riskyInsight = await _sut.ScoreAsync(risky, TestContext.Current.CancellationToken);
        var normalInsight = await _sut.ScoreAsync(normal, TestContext.Current.CancellationToken);

        Assert.True(riskyInsight.RiskScore > normalInsight.RiskScore);
    }

    [Fact]
    public async Task ScoreAsync_MultipleTagsPresent_ConfidenceJsonContainsAllTags()
    {
        var capture = MakeCapture(x =>
        {
            x.Host = "doubleclick.net";
            x.RequestBody = """{"email":"user@example.com"}""";
        });
        await SaveCapture(capture);

        var insight = await _sut.ScoreAsync(capture, TestContext.Current.CancellationToken);

        var confidence = JsonSerializer.Deserialize<Dictionary<string, double>>(insight.ConfidenceJson);
        Assert.NotNull(confidence);
        Assert.Contains("DataBroker", confidence!.Keys);
        Assert.Contains("PiiDetected", confidence.Keys);
    }
}
