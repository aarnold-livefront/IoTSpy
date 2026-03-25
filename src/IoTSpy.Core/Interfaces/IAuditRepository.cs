using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IAuditRepository
{
    Task AddAsync(AuditEntry entry, CancellationToken ct = default);
    Task<List<AuditEntry>> GetRecentAsync(int count = 100, CancellationToken ct = default);
    Task<List<AuditEntry>> GetByUserAsync(Guid userId, int count = 100, CancellationToken ct = default);
    Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default);
}
