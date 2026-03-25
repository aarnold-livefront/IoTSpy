using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IDashboardLayoutRepository
{
    Task<List<DashboardLayout>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<DashboardLayout?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<DashboardLayout> CreateAsync(DashboardLayout layout, CancellationToken ct = default);
    Task<DashboardLayout> UpdateAsync(DashboardLayout layout, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
