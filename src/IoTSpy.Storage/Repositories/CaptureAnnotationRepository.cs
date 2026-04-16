using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Storage.Repositories;

public class CaptureAnnotationRepository(IoTSpyDbContext db) : ICaptureAnnotationRepository
{
    public async Task<CaptureAnnotation?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.CaptureAnnotations.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<List<CaptureAnnotation>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default)
        => await db.CaptureAnnotations
            .Where(a => a.SessionId == sessionId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

    public async Task<List<CaptureAnnotation>> GetByCaptureAsync(Guid captureId, CancellationToken ct = default)
        => await db.CaptureAnnotations
            .Where(a => a.CaptureId == captureId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

    public async Task<CaptureAnnotation> AddAsync(CaptureAnnotation annotation, CancellationToken ct = default)
    {
        db.CaptureAnnotations.Add(annotation);
        await db.SaveChangesAsync(ct);
        return annotation;
    }

    public async Task UpdateAsync(CaptureAnnotation annotation, CancellationToken ct = default)
    {
        db.CaptureAnnotations.Update(annotation);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await db.CaptureAnnotations.Where(a => a.Id == id).ExecuteDeleteAsync(ct);
    }
}
