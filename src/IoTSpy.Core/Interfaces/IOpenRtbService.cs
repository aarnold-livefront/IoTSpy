using IoTSpy.Core.Enums;
using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IOpenRtbService
{
    bool IsOpenRtbRequest(string contentType, string path, string body);
    Task<bool> ProcessAndStripAsync(HttpMessage message, ManipulationPhase phase, CancellationToken ct = default);
    Task<IReadOnlyList<OpenRtbPiiPolicy>> GetPoliciesAsync(CancellationToken ct = default);
}
