using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IPiiStrippingLogRepository
{
    Task AddBatchAsync(IEnumerable<PiiStrippingLog> logs, CancellationToken ct = default);
    Task<List<PiiStrippingLog>> GetByCaptureIdAsync(Guid captureId, CancellationToken ct = default);
    Task<List<PiiStrippingLog>> GetPagedAsync(PiiLogFilter filter, int page, int pageSize, CancellationToken ct = default);
    Task<int> CountAsync(PiiLogFilter filter, CancellationToken ct = default);
    Task<PiiAuditStats> GetStatsAsync(PiiLogFilter filter, CancellationToken ct = default);
}

public record PiiLogFilter(
    string? HostContains = null,
    string? FieldPath = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null
);

public record PiiAuditStats(
    int TotalStripped,
    Dictionary<string, int> ByFieldPath,
    Dictionary<string, int> ByHost
);
