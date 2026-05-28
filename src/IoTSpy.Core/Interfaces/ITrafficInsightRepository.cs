using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface ITrafficInsightRepository
{
    Task<TrafficInsight?> GetByCaptureIdAsync(Guid captureId, CancellationToken ct = default);
    Task<List<TrafficInsight>> GetTriageQueueAsync(int page, int pageSize, bool unreviewedOnly, CancellationToken ct = default);
    Task<int> CountTriageQueueAsync(bool unreviewedOnly, CancellationToken ct = default);
    Task UpsertAsync(TrafficInsight insight, CancellationToken ct = default);
    Task MarkReviewedAsync(Guid id, Guid userId, bool dismissed, string? note, CancellationToken ct = default);
    Task<List<TrafficInsight>> GetByCaptureIdsAsync(IEnumerable<Guid> captureIds, CancellationToken ct = default);
}
