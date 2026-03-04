using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IManipulationRuleRepository
{
    Task<ManipulationRule> AddAsync(ManipulationRule rule, CancellationToken ct = default);
    Task<ManipulationRule?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<ManipulationRule>> GetAllAsync(CancellationToken ct = default);
    Task<List<ManipulationRule>> GetEnabledAsync(CancellationToken ct = default);
    Task<ManipulationRule> UpdateAsync(ManipulationRule rule, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
