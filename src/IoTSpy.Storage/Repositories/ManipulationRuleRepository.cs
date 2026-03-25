using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Storage.Repositories;

public class ManipulationRuleRepository(IoTSpyDbContext db) : IManipulationRuleRepository
{
    public async Task<ManipulationRule> AddAsync(ManipulationRule rule, CancellationToken ct = default)
    {
        db.ManipulationRules.Add(rule);
        await db.SaveChangesAsync(ct);
        return rule;
    }

    public Task<ManipulationRule?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.ManipulationRules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<List<ManipulationRule>> GetAllAsync(CancellationToken ct = default) =>
        db.ManipulationRules
            .AsNoTracking()
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.CreatedAt)
            .ToListAsync(ct);

    public Task<List<ManipulationRule>> GetEnabledAsync(CancellationToken ct = default) =>
        db.ManipulationRules
            .AsNoTracking()
            .Where(r => r.Enabled)
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.CreatedAt)
            .ToListAsync(ct);

    public async Task<ManipulationRule> UpdateAsync(ManipulationRule rule, CancellationToken ct = default)
    {
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        db.ManipulationRules.Update(rule);
        await db.SaveChangesAsync(ct);
        return rule;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var rule = await db.ManipulationRules.FindAsync([id], ct);
        if (rule is not null)
        {
            db.ManipulationRules.Remove(rule);
            await db.SaveChangesAsync(ct);
        }
    }
}
