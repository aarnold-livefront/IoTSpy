using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Storage.Repositories;

public class OpenRtbPiiPolicyRepository(IoTSpyDbContext db) : IOpenRtbPiiPolicyRepository
{
    public async Task<OpenRtbPiiPolicy> AddAsync(OpenRtbPiiPolicy policy, CancellationToken ct = default)
    {
        db.OpenRtbPiiPolicies.Add(policy);
        await db.SaveChangesAsync(ct);
        return policy;
    }

    public Task<OpenRtbPiiPolicy?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.OpenRtbPiiPolicies.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<List<OpenRtbPiiPolicy>> GetAllAsync(CancellationToken ct = default) =>
        db.OpenRtbPiiPolicies.OrderBy(p => p.Priority).ThenBy(p => p.CreatedAt).ToListAsync(ct);

    public Task<List<OpenRtbPiiPolicy>> GetEnabledAsync(CancellationToken ct = default) =>
        db.OpenRtbPiiPolicies
            .Where(p => p.Enabled)
            .OrderBy(p => p.Priority)
            .ThenBy(p => p.CreatedAt)
            .ToListAsync(ct);

    public async Task<OpenRtbPiiPolicy> UpdateAsync(OpenRtbPiiPolicy policy, CancellationToken ct = default)
    {
        policy.UpdatedAt = DateTimeOffset.UtcNow;
        db.OpenRtbPiiPolicies.Update(policy);
        await db.SaveChangesAsync(ct);
        return policy;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var policy = await db.OpenRtbPiiPolicies.FindAsync([id], ct);
        if (policy is not null)
        {
            db.OpenRtbPiiPolicies.Remove(policy);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        db.OpenRtbPiiPolicies.RemoveRange(db.OpenRtbPiiPolicies);
        await db.SaveChangesAsync(ct);
    }
}
