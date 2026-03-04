using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IReplaySessionRepository
{
    Task<ReplaySession> AddAsync(ReplaySession session, CancellationToken ct = default);
    Task<ReplaySession?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<ReplaySession>> GetByCaptureIdAsync(Guid captureId, CancellationToken ct = default);
    Task<List<ReplaySession>> GetAllAsync(int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
