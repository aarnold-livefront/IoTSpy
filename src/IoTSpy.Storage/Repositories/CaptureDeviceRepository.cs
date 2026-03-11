using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Storage.Repositories
{
    public class CaptureDeviceRepository : ICaptureDeviceRepository, IPacketRepository
    {
        private readonly IoTSpyDbContext _context;

        public CaptureDeviceRepository(IoTSpyDbContext context)
        {
            _context = context;
        }

        // ICaptureDeviceRepository methods (explicit interface implementation for GetByIdAsync to avoid ambiguity)
        Task<CaptureDevice?> ICaptureDeviceRepository.GetByIdAsync(Guid id) => GetDeviceByIdAsync(id);

        public async Task<CaptureDevice?> GetDeviceByIdAsync(Guid id)
        {
            return await _context.CaptureDevices.FindAsync(id);
        }

        public async Task<IEnumerable<CaptureDevice>> GetAllAsync()
        {
            return await _context.CaptureDevices.ToListAsync();
        }

        public async Task AddAsync(CaptureDevice device)
        {
            await _context.CaptureDevices.AddAsync(device);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(CaptureDevice device)
        {
            _context.CaptureDevices.Update(device);
            await _context.SaveChangesAsync();
        }

        Task ICaptureDeviceRepository.DeleteAsync(Guid id) => DeleteDeviceAsync(id);

        public async Task DeleteDeviceAsync(Guid id)
        {
            var device = await GetDeviceByIdAsync(id);
            if (device is not null)
            {
                _context.CaptureDevices.Remove(device);
                await _context.SaveChangesAsync();
            }
        }

        // IPacketRepository methods
        public async Task<CapturedPacket?> GetByIdAsync(Guid id)
        {
            return await _context.Packets.FindAsync(id);
        }

        public async Task<IEnumerable<CapturedPacket>> GetByDeviceIdAsync(Guid deviceId, int? limit = null)
        {
            var query = _context.Packets.Where(p => p.DeviceId == deviceId);
            if (limit.HasValue)
                return await query.OrderByDescending(p => p.Timestamp).Take(limit.Value).ToListAsync();
            return await query.OrderByDescending(p => p.Timestamp).ToListAsync();
        }

        public async Task<IEnumerable<CapturedPacket>> GetByProtocolAsync(Guid deviceId, string protocol)
        {
            return await _context.Packets
                .Where(p => p.DeviceId == deviceId && p.Protocol == protocol)
                .OrderByDescending(p => p.Timestamp)
                .ToListAsync();
        }

        public async Task<IEnumerable<CapturedPacket>> GetFilteredAsync(PacketFilter filter, int limit = 1000)
        {
            var query = _context.Packets.AsQueryable();

            if (filter.Protocol != null)
                query = query.Where(p => p.Protocol == filter.Protocol);
            
            if (filter.SourceIp != null)
                query = query.Where(p => p.SourceIp == filter.SourceIp);
            
            if (filter.DestinationIp != null)
                query = query.Where(p => p.DestinationIp == filter.DestinationIp);
            
            if (filter.SourcePort.HasValue)
                query = query.Where(p => p.SourcePort == filter.SourcePort);
            
            if (filter.DestinationPort.HasValue)
                query = query.Where(p => p.DestinationPort == filter.DestinationPort);
            
            if (filter.MacAddress != null)
                query = query.Where(p => p.SourceMac == filter.MacAddress || p.DestinationMac == filter.MacAddress);
            
            if (filter.ShowOnlyErrors)
                query = query.Where(p => p.IsError);
            
            if (filter.ShowOnlyRetransmissions)
                query = query.Where(p => p.IsRetransmission);
            
            if (filter.FromTime.HasValue)
                query = query.Where(p => p.Timestamp >= filter.FromTime.Value);
            
            if (filter.ToTime.HasValue)
                query = query.Where(p => p.Timestamp <= filter.ToTime.Value);
            
            if (filter.PayloadSearch != null)
                query = query.Where(p => p.PayloadPreview.Contains(filter.PayloadSearch));

            return await query.OrderByDescending(p => p.Timestamp).Take(limit).ToListAsync();
        }

        public async Task AddAsync(CapturedPacket packet)
        {
            await _context.Packets.AddAsync(packet);
            await _context.SaveChangesAsync();
        }

        Task IPacketRepository.DeleteAsync(Guid id) => DeletePacketAsync(id);

        public async Task DeletePacketAsync(Guid id)
        {
            var packet = await GetByIdAsync(id);
            if (packet is not null)
            {
                _context.Packets.Remove(packet);
                await _context.SaveChangesAsync();
            }
        }

        public async Task ClearAllAsync(Guid deviceId)
        {
            var packets = await GetByDeviceIdAsync(deviceId, limit: null);
            foreach (var packet in packets)
            {
                _context.Packets.Remove(packet);
            }
            await _context.SaveChangesAsync();
        }
    }
}
