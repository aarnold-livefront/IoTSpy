using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IOpenRtbPiiPolicyRepository
{
    Task<OpenRtbPiiPolicy> AddAsync(OpenRtbPiiPolicy policy, CancellationToken ct = default);
    Task<OpenRtbPiiPolicy?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<OpenRtbPiiPolicy>> GetAllAsync(CancellationToken ct = default);
    Task<List<OpenRtbPiiPolicy>> GetEnabledAsync(CancellationToken ct = default);
    Task<OpenRtbPiiPolicy> UpdateAsync(OpenRtbPiiPolicy policy, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
}
