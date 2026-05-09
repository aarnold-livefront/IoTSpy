using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace IoTSpy.Manipulation;

/// <summary>
/// IMemoryCache-backed cache for enabled manipulation rules with a 30-second sliding TTL.
/// Invalidated explicitly whenever a rule is created, updated, or deleted.
/// </summary>
public sealed class ManipulationRuleCache(
    IMemoryCache memoryCache,
    IServiceScopeFactory scopeFactory) : IManipulationRuleCache
{
    private const string CacheKey = "ManipulationRules:Enabled";
    private static readonly TimeSpan SlidingExpiry = TimeSpan.FromSeconds(30);

    public async Task<IReadOnlyList<ManipulationRule>> GetEnabledAsync(CancellationToken ct = default)
    {
        if (memoryCache.TryGetValue(CacheKey, out IReadOnlyList<ManipulationRule>? cached) && cached is not null)
            return cached;

        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IManipulationRuleRepository>();
        var rules = await repo.GetEnabledAsync(ct);

        memoryCache.Set(CacheKey, (IReadOnlyList<ManipulationRule>)rules, new MemoryCacheEntryOptions
        {
            SlidingExpiration = SlidingExpiry
        });

        return rules;
    }

    public void Invalidate() => memoryCache.Remove(CacheKey);
}
