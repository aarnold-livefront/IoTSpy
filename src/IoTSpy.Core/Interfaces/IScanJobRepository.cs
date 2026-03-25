using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IScanJobRepository
{
    Task<ScanJob> AddAsync(ScanJob job, CancellationToken ct = default);
    Task<ScanJob?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<ScanJob>> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct = default);
    Task<List<ScanJob>> GetAllAsync(int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<ScanJob> UpdateAsync(ScanJob job, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task AddFindingAsync(ScanFinding finding, CancellationToken ct = default);
    Task AddFindingsAsync(IEnumerable<ScanFinding> findings, CancellationToken ct = default);
    Task<List<ScanFinding>> GetFindingsAsync(Guid scanJobId, CancellationToken ct = default);
}
