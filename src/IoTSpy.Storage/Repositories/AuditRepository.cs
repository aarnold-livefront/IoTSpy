using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Storage.Repositories;

public class AuditRepository(IoTSpyDbContext db) : IAuditRepository
{
    public async Task AddAsync(AuditEntry entry, CancellationToken ct = default)
    {
        db.AuditEntries.Add(entry);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<AuditEntry>> GetRecentAsync(int count = 100, CancellationToken ct = default)
        => await db.AuditEntries
            .OrderByDescending(a => a.Timestamp)
            .Take(count)
            .ToListAsync(ct);

    public async Task<List<AuditEntry>> GetByUserAsync(Guid userId, int count = 100, CancellationToken ct = default)
        => await db.AuditEntries
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Timestamp)
            .Take(count)
            .ToListAsync(ct);

    public async Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default)
        => await db.AuditEntries.Where(a => a.Timestamp < cutoff).ExecuteDeleteAsync(ct);

    public async Task<int> ArchiveOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default)
    {
        var entries = await db.AuditEntries
            .Where(a => a.Timestamp < cutoff)
            .ToListAsync(ct);

        if (entries.Count == 0) return 0;

        var now = DateTimeOffset.UtcNow;
        var archiveRows = entries.Select(a => new AuditArchiveEntry
        {
            Id = a.Id,
            UserId = a.UserId,
            Username = a.Username,
            Action = a.Action,
            EntityType = a.EntityType,
            EntityId = a.EntityId,
            Details = a.Details,
            OldValue = a.OldValue,
            NewValue = a.NewValue,
            IpAddress = a.IpAddress,
            Timestamp = a.Timestamp,
            ArchivedAt = now
        }).ToList();

        var ids = entries.Select(a => a.Id).ToList();

        // Insert and delete in a single transaction; delete targets exact IDs to avoid
        // racing with new audit entries written between the load and the delete.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.AuditArchive.AddRange(archiveRows);
        await db.SaveChangesAsync(ct);
        await db.AuditEntries.Where(a => ids.Contains(a.Id)).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);

        return entries.Count;
    }

    public async Task<int> PurgeArchiveOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default)
        => await db.AuditArchive.Where(a => a.ArchivedAt < cutoff).ExecuteDeleteAsync(ct);

    public async Task<AuditStats> GetStatsAsync(CancellationToken ct = default)
    {
        var mainCount = await db.AuditEntries.CountAsync(ct);
        var archiveCount = await db.AuditArchive.CountAsync(ct);
        var oldestMain = mainCount > 0
            ? await db.AuditEntries.MinAsync(a => (DateTimeOffset?)a.Timestamp, ct)
            : null;
        var oldestArchive = archiveCount > 0
            ? await db.AuditArchive.MinAsync(a => (DateTimeOffset?)a.Timestamp, ct)
            : null;
        return new AuditStats(mainCount, archiveCount, oldestMain, oldestArchive);
    }
}
