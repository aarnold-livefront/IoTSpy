using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Storage.Repositories;

public class OpenRtbEventRepository(IoTSpyDbContext db) : IOpenRtbEventRepository
{
    public async Task<OpenRtbEvent> AddAsync(OpenRtbEvent evt, CancellationToken ct = default)
    {
        db.OpenRtbEvents.Add(evt);
        await db.SaveChangesAsync(ct);
        return evt;
    }

    public Task<OpenRtbEvent?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.OpenRtbEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<List<OpenRtbEvent>> GetPagedAsync(
        OpenRtbEventFilter filter, int page, int pageSize, CancellationToken ct = default)
    {
        return ApplyFilter(filter)
            .OrderByDescending(e => e.DetectedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public Task<int> CountAsync(OpenRtbEventFilter filter, CancellationToken ct = default) =>
        ApplyFilter(filter).CountAsync(ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var evt = await db.OpenRtbEvents.FindAsync([id], ct);
        if (evt is not null)
        {
            db.OpenRtbEvents.Remove(evt);
            await db.SaveChangesAsync(ct);
        }
    }

    private IQueryable<OpenRtbEvent> ApplyFilter(OpenRtbEventFilter filter)
    {
        var q = db.OpenRtbEvents.AsNoTracking();

        if (!string.IsNullOrEmpty(filter.HostContains))
            q = q.Where(e => e.Exchange.Contains(filter.HostContains));
        if (filter.MessageType.HasValue)
            q = q.Where(e => e.MessageType == filter.MessageType);
        if (filter.From.HasValue)
            q = q.Where(e => e.DetectedAt >= filter.From);
        if (filter.To.HasValue)
            q = q.Where(e => e.DetectedAt <= filter.To);
        if (filter.HasPii.HasValue)
            q = filter.HasPii.Value
                ? q.Where(e => e.HasDeviceInfo || e.HasUserData || e.HasGeoData)
                : q.Where(e => !e.HasDeviceInfo && !e.HasUserData && !e.HasGeoData);

        return q;
    }
}
