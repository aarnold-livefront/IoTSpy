using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Storage.Repositories;

public class DashboardLayoutRepository(IoTSpyDbContext db) : IDashboardLayoutRepository
{
    public async Task<List<DashboardLayout>> GetByUserAsync(Guid userId, CancellationToken ct = default)
        => await db.DashboardLayouts
            .Where(d => d.UserId == userId)
            .OrderBy(d => d.Name)
            .ToListAsync(ct);

    public async Task<DashboardLayout?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.DashboardLayouts.FindAsync([id], ct);

    public async Task<DashboardLayout> CreateAsync(DashboardLayout layout, CancellationToken ct = default)
    {
        db.DashboardLayouts.Add(layout);
        await db.SaveChangesAsync(ct);
        return layout;
    }

    public async Task<DashboardLayout> UpdateAsync(DashboardLayout layout, CancellationToken ct = default)
    {
        layout.UpdatedAt = DateTimeOffset.UtcNow;
        db.DashboardLayouts.Update(layout);
        await db.SaveChangesAsync(ct);
        return layout;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var layout = await db.DashboardLayouts.FindAsync([id], ct);
        if (layout is not null)
        {
            db.DashboardLayouts.Remove(layout);
            await db.SaveChangesAsync(ct);
        }
    }
}
