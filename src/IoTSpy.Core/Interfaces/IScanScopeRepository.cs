using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IScanScopeRepository
{
    Task<ScanScope> AddAsync(ScanScope scope, CancellationToken ct = default);
    Task<List<ScanScope>> GetAllAsync(CancellationToken ct = default);
    Task<List<ScanScope>> GetActiveAsync(CancellationToken ct = default);
    Task<ScanScope?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task UpdateAsync(ScanScope scope, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
