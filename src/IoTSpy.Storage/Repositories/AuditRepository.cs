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
}
