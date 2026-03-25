using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IApiSpecRepository
{
    Task<List<ApiSpecDocument>> GetAllAsync(CancellationToken ct = default);
    Task<ApiSpecDocument?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiSpecDocument?> GetActiveForHostAsync(string host, CancellationToken ct = default);
    Task<ApiSpecDocument> CreateAsync(ApiSpecDocument doc, CancellationToken ct = default);
    Task<ApiSpecDocument> UpdateAsync(ApiSpecDocument doc, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<List<ContentReplacementRule>> GetReplacementRulesAsync(Guid specId, CancellationToken ct = default);
    Task<ContentReplacementRule> AddReplacementRuleAsync(ContentReplacementRule rule, CancellationToken ct = default);
    Task<ContentReplacementRule> UpdateReplacementRuleAsync(ContentReplacementRule rule, CancellationToken ct = default);
    Task DeleteReplacementRuleAsync(Guid ruleId, CancellationToken ct = default);
}
