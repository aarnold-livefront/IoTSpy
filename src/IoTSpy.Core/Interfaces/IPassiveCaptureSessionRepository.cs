using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IPassiveCaptureSessionRepository
{
    Task<IReadOnlyList<PassiveCaptureSession>> ListAsync(CancellationToken ct = default);
    Task<PassiveCaptureSession?> GetAsync(Guid id, CancellationToken ct = default);
    Task<PassiveCaptureSession> SaveSessionAsync(
        PassiveCaptureSession session,
        IEnumerable<CapturedRequest> captures,
        CancellationToken ct = default);
    Task<IReadOnlyList<CapturedRequest>> GetCapturesAsync(Guid sessionId, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
