using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IFuzzerJobRepository
{
    Task<FuzzerJob> AddAsync(FuzzerJob job, CancellationToken ct = default);
    Task<FuzzerJob?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<FuzzerJob>> GetAllAsync(int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<FuzzerJob> UpdateAsync(FuzzerJob job, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task AddResultAsync(FuzzerResult result, CancellationToken ct = default);
    Task<List<FuzzerResult>> GetResultsAsync(Guid jobId, CancellationToken ct = default);
}
