using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IBreakpointRepository
{
    Task<Breakpoint> AddAsync(Breakpoint breakpoint, CancellationToken ct = default);
    Task<Breakpoint?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Breakpoint>> GetAllAsync(CancellationToken ct = default);
    Task<List<Breakpoint>> GetEnabledAsync(CancellationToken ct = default);
    Task<Breakpoint> UpdateAsync(Breakpoint breakpoint, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
