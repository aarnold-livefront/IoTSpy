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
        // GetOrCreateAsync handles the race the previous TryGetValue+Set pattern
        // had: under concurrent traffic two requests could both miss, both query
        // the DB, and a parallel Invalidate() between TryGetValue and Set could
        // be silently overwritten — leaving stale rules cached for up to 30 s.
        var cached = await memoryCache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.SlidingExpiration = SlidingExpiry;
            using var scope = scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IManipulationRuleRepository>();
            var rules = await repo.GetEnabledAsync(ct);
            return (IReadOnlyList<ManipulationRule>)rules;
        });

        return cached ?? [];
    }

    public void Invalidate() => memoryCache.Remove(CacheKey);
}
