using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Storage.Repositories;

public class DeviceRepository(IoTSpyDbContext db) : IDeviceRepository
{
    public Task<List<Device>> GetAllAsync(CancellationToken ct = default) =>
        db.Devices.OrderByDescending(d => d.LastSeen).ToListAsync(ct);

    public Task<Device?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Devices.FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<Device?> GetByIpAsync(string ip, CancellationToken ct = default) =>
        db.Devices.FirstOrDefaultAsync(d => d.IpAddress == ip, ct);

    public async Task<Device> UpsertByIpAsync(Device device, CancellationToken ct = default)
    {
        var existing = await db.Devices.FirstOrDefaultAsync(d => d.IpAddress == device.IpAddress, ct);
        if (existing is null)
        {
            db.Devices.Add(device);
            await db.SaveChangesAsync(ct);
            return device;
        }

        existing.LastSeen = DateTimeOffset.UtcNow;
        if (!string.IsNullOrEmpty(device.Hostname) && existing.Hostname != device.Hostname)
            existing.Hostname = device.Hostname;
        if (!string.IsNullOrEmpty(device.Vendor) && existing.Vendor != device.Vendor)
            existing.Vendor = device.Vendor;
        if (!string.IsNullOrEmpty(device.MacAddress) && existing.MacAddress != device.MacAddress)
            existing.MacAddress = device.MacAddress;

        await db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<Device> UpdateAsync(Device device, CancellationToken ct = default)
    {
        db.Devices.Update(device);
        await db.SaveChangesAsync(ct);
        return device;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var device = await db.Devices.FindAsync([id], ct);
        if (device is not null)
        {
            db.Devices.Remove(device);
            await db.SaveChangesAsync(ct);
        }
    }
}
