using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface ISessionActivityRepository
{
    Task<List<SessionActivity>> GetBySessionAsync(Guid sessionId, int count = 100, CancellationToken ct = default);
    Task AddAsync(SessionActivity activity, CancellationToken ct = default);
}
