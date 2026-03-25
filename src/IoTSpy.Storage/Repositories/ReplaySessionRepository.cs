using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Storage.Repositories;

public class ReplaySessionRepository(IoTSpyDbContext db) : IReplaySessionRepository
{
    public async Task<ReplaySession> AddAsync(ReplaySession session, CancellationToken ct = default)
    {
        db.ReplaySessions.Add(session);
        await db.SaveChangesAsync(ct);
        return session;
    }

    public Task<ReplaySession?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.ReplaySessions
            .AsNoTracking()
            .Include(r => r.OriginalCapture)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<List<ReplaySession>> GetByCaptureIdAsync(Guid captureId, CancellationToken ct = default) =>
        db.ReplaySessions
            .AsNoTracking()
            .Where(r => r.OriginalCaptureId == captureId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

    public Task<List<ReplaySession>> GetAllAsync(int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        db.ReplaySessions
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var session = await db.ReplaySessions.FindAsync([id], ct);
        if (session is not null)
        {
            db.ReplaySessions.Remove(session);
            await db.SaveChangesAsync(ct);
        }
    }
}
