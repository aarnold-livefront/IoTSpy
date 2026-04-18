using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Storage.Repositories;

public class PassiveCaptureSessionRepository(IoTSpyDbContext db) : IPassiveCaptureSessionRepository
{
    public async Task<IReadOnlyList<PassiveCaptureSession>> ListAsync(CancellationToken ct = default) =>
        await db.PassiveCaptureSessions
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

    public async Task<PassiveCaptureSession?> GetAsync(Guid id, CancellationToken ct = default) =>
        await db.PassiveCaptureSessions.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<PassiveCaptureSession> SaveSessionAsync(
        PassiveCaptureSession session,
        IEnumerable<CapturedRequest> captures,
        CancellationToken ct = default)
    {
        var captureList = captures.ToList();
        session.EntryCount = captureList.Count;
        foreach (var c in captureList)
        {
            c.Id = Guid.NewGuid();
            c.PassiveCaptureSessionId = session.Id;
        }

        db.PassiveCaptureSessions.Add(session);
        db.Captures.AddRange(captureList);
        await db.SaveChangesAsync(ct);
        return session;
    }

    public async Task<IReadOnlyList<CapturedRequest>> GetCapturesAsync(Guid sessionId, CancellationToken ct = default) =>
        await db.Captures
            .Where(c => c.PassiveCaptureSessionId == sessionId)
            .OrderBy(c => c.Timestamp)
            .ToListAsync(ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var session = await db.PassiveCaptureSessions.FindAsync([id], ct);
        if (session is not null)
        {
            db.PassiveCaptureSessions.Remove(session);
            await db.SaveChangesAsync(ct);
        }
    }
}
