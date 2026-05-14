using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IAuditRepository
{
    Task AddAsync(AuditEntry entry, CancellationToken ct = default);
    Task<List<AuditEntry>> GetRecentAsync(int count = 100, CancellationToken ct = default);
    Task<List<AuditEntry>> GetByUserAsync(Guid userId, int count = 100, CancellationToken ct = default);
    Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default);

    /// <summary>
    /// Copies entries older than <paramref name="cutoff"/> from AuditEntries to AuditArchive,
    /// then deletes them from the main table. Returns the count moved.
    /// </summary>
    Task<int> ArchiveOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default);

    /// <summary>Hard-deletes archive rows older than <paramref name="cutoff"/>. Returns count deleted.</summary>
    Task<int> PurgeArchiveOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default);

    Task<AuditStats> GetStatsAsync(CancellationToken ct = default);
}

public record AuditStats(
    int MainCount,
    int ArchiveCount,
    DateTimeOffset? OldestMainTimestamp,
    DateTimeOffset? OldestArchiveTimestamp);
