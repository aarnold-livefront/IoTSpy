using IoTSpy.Core.Models;

namespace IoTSpy.Analytics.Services;

public interface IInsightService
{
    Task<TrafficInsight> ScoreAsync(CapturedRequest capture, CancellationToken ct = default);
    Task<TrafficInsight> ScoreByCaptureIdAsync(Guid captureId, CancellationToken ct = default);
}
