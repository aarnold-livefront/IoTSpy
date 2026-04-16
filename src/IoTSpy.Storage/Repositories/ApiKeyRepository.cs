using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Storage.Repositories;

public class ApiKeyRepository(IoTSpyDbContext db) : IApiKeyRepository
{
    public async Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.ApiKeys.AsNoTracking().FirstOrDefaultAsync(k => k.Id == id, ct);

    public async Task<ApiKey?> GetByHashAsync(string hash, CancellationToken ct = default)
        => await db.ApiKeys.AsNoTracking().FirstOrDefaultAsync(k => k.KeyHash == hash, ct);

    public async Task<List<ApiKey>> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default)
        => await db.ApiKeys.AsNoTracking()
            .Where(k => k.OwnerId == ownerId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);

    public async Task<List<ApiKey>> GetAllAsync(CancellationToken ct = default)
        => await db.ApiKeys.AsNoTracking()
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);

    public async Task<ApiKey> CreateAsync(ApiKey key, CancellationToken ct = default)
    {
        db.ApiKeys.Add(key);
        await db.SaveChangesAsync(ct);
        return key;
    }

    public async Task<ApiKey> UpdateAsync(ApiKey key, CancellationToken ct = default)
    {
        db.ApiKeys.Update(key);
        await db.SaveChangesAsync(ct);
        return key;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var key = await db.ApiKeys.FindAsync([id], ct);
        if (key is not null)
        {
            db.ApiKeys.Remove(key);
            await db.SaveChangesAsync(ct);
        }
    }
}
