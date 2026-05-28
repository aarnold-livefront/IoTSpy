using System.Text.Json;
using IoTSpy.Analytics.Rules;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Analytics.Services;

public sealed class InsightService(
    RuleBasedTagger ruleTagger,
    ITrafficInsightRepository insightRepo,
    ICaptureRepository captureRepo,
    ILogger<InsightService> logger) : IInsightService
{
    // ExfiltrationRisk and PiiDetected carry higher weight in the composite score
    private static readonly Dictionary<RiskTag, double> TagWeights = new()
    {
        [RiskTag.ExfiltrationRisk] = 1.5,
        [RiskTag.PiiDetected] = 1.5,
        [RiskTag.MqttCredentialExposure] = 1.3,
        [RiskTag.DnsTunneling] = 1.2,
        [RiskTag.DataBroker] = 1.0,
        [RiskTag.SuspiciousTls] = 1.0,
        [RiskTag.HighEntropyPayload] = 1.0,
        [RiskTag.UnusualPort] = 0.8
    };

    private const string ModelVersion = "rule-v1";

    public async Task<TrafficInsight> ScoreAsync(CapturedRequest capture, CancellationToken ct = default)
    {
        var ruleTags = ruleTagger.Tag(capture);
        var confidence = ruleTags.ToDictionary(t => t.Tag, t => t.Confidence);
        var riskScore = ComputeRiskScore(confidence);
        var source = confidence.Count == 0 ? "rule" : "rule";

        var insight = new TrafficInsight
        {
            CaptureId = capture.Id,
            TagsJson = JsonSerializer.Serialize(confidence.Keys.Select(t => t.ToString()).ToArray()),
            ConfidenceJson = JsonSerializer.Serialize(
                confidence.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)),
            RiskScore = riskScore,
            ModelVersion = ModelVersion,
            Source = source,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await insightRepo.UpsertAsync(insight, ct);
        logger.LogDebug("Scored capture {CaptureId}: {TagCount} tags, risk={RiskScore:F2}",
            capture.Id, confidence.Count, riskScore);

        return insight;
    }

    public async Task<TrafficInsight> ScoreByCaptureIdAsync(Guid captureId, CancellationToken ct = default)
    {
        var capture = await captureRepo.GetByIdAsync(captureId, ct);
        if (capture is null)
            throw new KeyNotFoundException($"Capture {captureId} not found.");
        return await ScoreAsync(capture, ct);
    }

    private static double ComputeRiskScore(Dictionary<RiskTag, double> confidence)
    {
        if (confidence.Count == 0) return 0.0;

        var weightedSum = confidence.Sum(kv =>
            kv.Value * TagWeights.GetValueOrDefault(kv.Key, 1.0));
        var maxPossible = TagWeights.Values.Sum();

        return Math.Clamp(weightedSum / maxPossible, 0.0, 1.0);
    }
}
