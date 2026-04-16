using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IInvestigationSessionRepository
{
    Task<InvestigationSession?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<InvestigationSession?> GetByShareTokenAsync(string token, CancellationToken ct = default);
    Task<List<InvestigationSession>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<InvestigationSession> CreateAsync(InvestigationSession session, CancellationToken ct = default);
    Task UpdateAsync(InvestigationSession session, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    // SessionCapture join table
    Task AddCaptureAsync(SessionCapture sc, CancellationToken ct = default);
    Task RemoveCaptureAsync(Guid sessionId, Guid captureId, CancellationToken ct = default);
    Task<List<SessionCapture>> GetSessionCapturesAsync(Guid sessionId, CancellationToken ct = default);
    Task<bool> ContainsCaptureAsync(Guid sessionId, Guid captureId, CancellationToken ct = default);
}
