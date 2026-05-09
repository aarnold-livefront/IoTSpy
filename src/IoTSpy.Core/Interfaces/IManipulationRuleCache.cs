using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

/// <summary>
/// Short-lived cache for enabled manipulation rules. Reduces database round-trips
/// on every proxied request. Call <see cref="Invalidate"/> after any rule mutation.
/// </summary>
public interface IManipulationRuleCache
{
    /// <summary>Returns the cached list of enabled rules, fetching from the repository on a cache miss.</summary>
    Task<IReadOnlyList<ManipulationRule>> GetEnabledAsync(CancellationToken ct = default);

    /// <summary>Evicts the cached entry so the next call re-fetches from the database.</summary>
    void Invalidate();
}
