using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces
{
    public interface ICaptureDeviceRepository
    {
        Task<CaptureDevice?> GetByIdAsync(Guid id);
        Task<IEnumerable<CaptureDevice>> GetAllAsync();
        Task AddAsync(CaptureDevice device);
        Task UpdateAsync(CaptureDevice device);
        Task DeleteAsync(Guid id);
    }

    public interface IPacketRepository
    {
        Task<CapturedPacket?> GetByIdAsync(Guid id);
        Task<IEnumerable<CapturedPacket>> GetByDeviceIdAsync(Guid deviceId, int? limit = null);
        Task<IEnumerable<CapturedPacket>> GetByProtocolAsync(Guid deviceId, string protocol);
        Task<IEnumerable<CapturedPacket>> GetFilteredAsync(PacketFilter filter, int limit = 1000);
        Task AddAsync(CapturedPacket packet);
        Task DeleteAsync(Guid id);
        Task ClearAllAsync(Guid deviceId);
    }
}
