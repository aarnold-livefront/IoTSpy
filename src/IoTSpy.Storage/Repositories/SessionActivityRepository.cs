using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Storage.Repositories;

public class SessionActivityRepository(IoTSpyDbContext db) : ISessionActivityRepository
{
    public async Task<List<SessionActivity>> GetBySessionAsync(Guid sessionId, int count = 100, CancellationToken ct = default)
        => await db.SessionActivities
            .Where(a => a.SessionId == sessionId)
            .OrderByDescending(a => a.Timestamp)
            .Take(count)
            .ToListAsync(ct);

    public async Task AddAsync(SessionActivity activity, CancellationToken ct = default)
    {
        db.SessionActivities.Add(activity);
        await db.SaveChangesAsync(ct);
    }
}
