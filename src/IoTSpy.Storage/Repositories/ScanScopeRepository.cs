using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Storage.Repositories;

public class ScanScopeRepository(IoTSpyDbContext db) : IScanScopeRepository
{
    public async Task<ScanScope> AddAsync(ScanScope scope, CancellationToken ct = default)
    {
        db.ScanScopes.Add(scope);
        await db.SaveChangesAsync(ct);
        return scope;
    }

    public Task<List<ScanScope>> GetAllAsync(CancellationToken ct = default) =>
        db.ScanScopes.AsNoTracking().OrderByDescending(s => s.CreatedAt).ToListAsync(ct);

    public Task<List<ScanScope>> GetActiveAsync(CancellationToken ct = default) =>
        db.ScanScopes.AsNoTracking().Where(s => s.IsActive).ToListAsync(ct);

    public Task<ScanScope?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.ScanScopes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task UpdateAsync(ScanScope scope, CancellationToken ct = default)
    {
        db.ScanScopes.Update(scope);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await db.ScanScopes.Where(s => s.Id == id).ExecuteDeleteAsync(ct);
    }
}
