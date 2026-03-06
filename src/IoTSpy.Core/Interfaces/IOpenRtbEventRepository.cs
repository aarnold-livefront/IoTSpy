using IoTSpy.Core.Enums;
using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IOpenRtbEventRepository
{
    Task<OpenRtbEvent> AddAsync(OpenRtbEvent evt, CancellationToken ct = default);
    Task<OpenRtbEvent?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<OpenRtbEvent>> GetPagedAsync(OpenRtbEventFilter filter, int page, int pageSize, CancellationToken ct = default);
    Task<int> CountAsync(OpenRtbEventFilter filter, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public record OpenRtbEventFilter(
    string? HostContains = null,
    OpenRtbMessageType? MessageType = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    bool? HasPii = null
);
