using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Storage.Repositories;

public class PiiStrippingLogRepository(IoTSpyDbContext db) : IPiiStrippingLogRepository
{
    public async Task AddBatchAsync(IEnumerable<PiiStrippingLog> logs, CancellationToken ct = default)
    {
        db.PiiStrippingLogs.AddRange(logs);
        await db.SaveChangesAsync(ct);
    }

    public Task<List<PiiStrippingLog>> GetByCaptureIdAsync(Guid captureId, CancellationToken ct = default) =>
        db.PiiStrippingLogs
            .Where(l => l.CapturedRequestId == captureId)
            .OrderByDescending(l => l.StrippedAt)
            .ToListAsync(ct);

    public Task<List<PiiStrippingLog>> GetPagedAsync(
        PiiLogFilter filter, int page, int pageSize, CancellationToken ct = default)
    {
        return ApplyFilter(filter)
            .OrderByDescending(l => l.StrippedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public Task<int> CountAsync(PiiLogFilter filter, CancellationToken ct = default) =>
        ApplyFilter(filter).CountAsync(ct);

    public async Task<PiiAuditStats> GetStatsAsync(PiiLogFilter filter, CancellationToken ct = default)
    {
        var query = ApplyFilter(filter);

        var total = await query.CountAsync(ct);

        var byFieldPath = await query
            .GroupBy(l => l.FieldPath)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, ct);

        var byHost = await query
            .GroupBy(l => l.Host)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, ct);

        return new PiiAuditStats(total, byFieldPath, byHost);
    }

    private IQueryable<PiiStrippingLog> ApplyFilter(PiiLogFilter filter)
    {
        var q = db.PiiStrippingLogs.AsQueryable();

        if (!string.IsNullOrEmpty(filter.HostContains))
            q = q.Where(l => l.Host.Contains(filter.HostContains));
        if (!string.IsNullOrEmpty(filter.FieldPath))
            q = q.Where(l => l.FieldPath == filter.FieldPath);
        if (filter.From.HasValue)
            q = q.Where(l => l.StrippedAt >= filter.From);
        if (filter.To.HasValue)
            q = q.Where(l => l.StrippedAt <= filter.To);

        return q;
    }
}
