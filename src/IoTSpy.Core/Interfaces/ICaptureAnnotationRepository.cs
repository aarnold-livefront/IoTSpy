using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface ICaptureAnnotationRepository
{
    Task<CaptureAnnotation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<CaptureAnnotation>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default);
    Task<List<CaptureAnnotation>> GetByCaptureAsync(Guid captureId, CancellationToken ct = default);
    Task<CaptureAnnotation> AddAsync(CaptureAnnotation annotation, CancellationToken ct = default);
    Task UpdateAsync(CaptureAnnotation annotation, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
