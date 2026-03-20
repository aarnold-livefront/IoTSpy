using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IScheduledScanRepository
{
    Task<List<ScheduledScan>> GetAllAsync(CancellationToken ct = default);
    Task<List<ScheduledScan>> GetEnabledAsync(CancellationToken ct = default);
    Task<ScheduledScan?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ScheduledScan> AddAsync(ScheduledScan scan, CancellationToken ct = default);
    Task<ScheduledScan> UpdateAsync(ScheduledScan scan, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
