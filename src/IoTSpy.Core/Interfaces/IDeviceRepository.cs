using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IDeviceRepository
{
    Task<List<Device>> GetAllAsync(CancellationToken ct = default);
    Task<Device?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Device?> GetByIpAsync(string ip, CancellationToken ct = default);
    Task<Device> UpsertByIpAsync(Device device, CancellationToken ct = default);
    Task<Device> UpdateAsync(Device device, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
