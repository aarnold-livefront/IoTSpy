using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Storage.Repositories;

public class BreakpointRepository(IoTSpyDbContext db) : IBreakpointRepository
{
    public async Task<Breakpoint> AddAsync(Breakpoint breakpoint, CancellationToken ct = default)
    {
        db.Breakpoints.Add(breakpoint);
        await db.SaveChangesAsync(ct);
        return breakpoint;
    }

    public Task<Breakpoint?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Breakpoints.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);

    public Task<List<Breakpoint>> GetAllAsync(CancellationToken ct = default) =>
        db.Breakpoints.AsNoTracking().OrderBy(b => b.CreatedAt).ToListAsync(ct);

    public Task<List<Breakpoint>> GetEnabledAsync(CancellationToken ct = default) =>
        db.Breakpoints.AsNoTracking().Where(b => b.Enabled).OrderBy(b => b.CreatedAt).ToListAsync(ct);

    public async Task<Breakpoint> UpdateAsync(Breakpoint breakpoint, CancellationToken ct = default)
    {
        breakpoint.UpdatedAt = DateTimeOffset.UtcNow;
        db.Breakpoints.Update(breakpoint);
        await db.SaveChangesAsync(ct);
        return breakpoint;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var bp = await db.Breakpoints.FindAsync([id], ct);
        if (bp is not null)
        {
            db.Breakpoints.Remove(bp);
            await db.SaveChangesAsync(ct);
        }
    }
}
