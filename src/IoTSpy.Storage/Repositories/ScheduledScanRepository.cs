using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Storage.Repositories;

public class ScheduledScanRepository(IoTSpyDbContext db) : IScheduledScanRepository
{
    public Task<List<ScheduledScan>> GetAllAsync(CancellationToken ct = default) =>
        db.ScheduledScans.Include(s => s.Device).OrderBy(s => s.CreatedAt).ToListAsync(ct);

    public Task<List<ScheduledScan>> GetEnabledAsync(CancellationToken ct = default) =>
        db.ScheduledScans.Where(s => s.IsEnabled).ToListAsync(ct);

    public Task<ScheduledScan?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.ScheduledScans.Include(s => s.Device).FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<ScheduledScan> AddAsync(ScheduledScan scan, CancellationToken ct = default)
    {
        db.ScheduledScans.Add(scan);
        await db.SaveChangesAsync(ct);
        return scan;
    }

    public async Task<ScheduledScan> UpdateAsync(ScheduledScan scan, CancellationToken ct = default)
    {
        db.ScheduledScans.Update(scan);
        await db.SaveChangesAsync(ct);
        return scan;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var scan = await db.ScheduledScans.FindAsync([id], ct);
        if (scan is not null)
        {
            db.ScheduledScans.Remove(scan);
            await db.SaveChangesAsync(ct);
        }
    }
}
