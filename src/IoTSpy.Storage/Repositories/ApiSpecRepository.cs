using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Storage.Repositories;

public class ApiSpecRepository(IoTSpyDbContext db) : IApiSpecRepository
{
    public Task<List<ApiSpecDocument>> GetAllAsync(CancellationToken ct = default) =>
        db.ApiSpecDocuments
            .Include(d => d.ReplacementRules)
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync(ct);

    public Task<ApiSpecDocument?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.ApiSpecDocuments
            .Include(d => d.ReplacementRules)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<ApiSpecDocument?> GetActiveForHostAsync(string host, CancellationToken ct = default) =>
        db.ApiSpecDocuments
            .Include(d => d.ReplacementRules)
            .FirstOrDefaultAsync(d =>
                d.Host == host &&
                d.Status == ApiSpecStatus.Active &&
                d.MockEnabled, ct);

    public async Task<ApiSpecDocument> CreateAsync(ApiSpecDocument doc, CancellationToken ct = default)
    {
        db.ApiSpecDocuments.Add(doc);
        await db.SaveChangesAsync(ct);
        return doc;
    }

    public async Task<ApiSpecDocument> UpdateAsync(ApiSpecDocument doc, CancellationToken ct = default)
    {
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        db.ApiSpecDocuments.Update(doc);
        await db.SaveChangesAsync(ct);
        return doc;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var doc = await db.ApiSpecDocuments.FindAsync([id], ct);
        if (doc is not null)
        {
            db.ApiSpecDocuments.Remove(doc);
            await db.SaveChangesAsync(ct);
        }
    }

    public Task<ContentReplacementRule?> GetRuleByIdAsync(Guid ruleId, CancellationToken ct = default) =>
        db.ContentReplacementRules.FirstOrDefaultAsync(r => r.Id == ruleId, ct);

    public Task<List<ContentReplacementRule>> GetReplacementRulesAsync(Guid specId, CancellationToken ct = default) =>
        db.ContentReplacementRules
            .Where(r => r.ApiSpecDocumentId == specId)
            .OrderBy(r => r.Priority)
            .ToListAsync(ct);

    public Task<List<ContentReplacementRule>> GetStandaloneRulesForHostAsync(string host, CancellationToken ct = default) =>
        db.ContentReplacementRules
            .Where(r => r.ApiSpecDocumentId == null && r.Host == host)
            .OrderBy(r => r.Priority)
            .ToListAsync(ct);

    public Task<List<ContentReplacementRule>> GetAllStandaloneRulesAsync(CancellationToken ct = default) =>
        db.ContentReplacementRules
            .Where(r => r.ApiSpecDocumentId == null)
            .OrderBy(r => r.Host).ThenBy(r => r.Priority)
            .ToListAsync(ct);

    public async Task<ContentReplacementRule> AddReplacementRuleAsync(ContentReplacementRule rule, CancellationToken ct = default)
    {
        db.ContentReplacementRules.Add(rule);
        await db.SaveChangesAsync(ct);
        return rule;
    }

    public async Task<ContentReplacementRule> UpdateReplacementRuleAsync(ContentReplacementRule rule, CancellationToken ct = default)
    {
        db.ContentReplacementRules.Update(rule);
        await db.SaveChangesAsync(ct);
        return rule;
    }

    public async Task DeleteReplacementRuleAsync(Guid ruleId, CancellationToken ct = default)
    {
        var rule = await db.ContentReplacementRules.FindAsync([ruleId], ct);
        if (rule is not null)
        {
            db.ContentReplacementRules.Remove(rule);
            await db.SaveChangesAsync(ct);
        }
    }
}
