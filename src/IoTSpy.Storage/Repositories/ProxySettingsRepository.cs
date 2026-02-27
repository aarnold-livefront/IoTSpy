using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Storage.Repositories;

public class ProxySettingsRepository(IoTSpyDbContext db) : IProxySettingsRepository
{
    public async Task<ProxySettings> GetAsync(CancellationToken ct = default)
    {
        var settings = await db.ProxySettings.FirstOrDefaultAsync(ct);
        if (settings is not null) return settings;

        // First-run defaults
        settings = new ProxySettings();
        db.ProxySettings.Add(settings);
        await db.SaveChangesAsync(ct);
        return settings;
    }

    public async Task<ProxySettings> SaveAsync(ProxySettings settings, CancellationToken ct = default)
    {
        var existing = await db.ProxySettings.FirstOrDefaultAsync(ct);
        if (existing is null)
        {
            db.ProxySettings.Add(settings);
        }
        else
        {
            db.Entry(existing).CurrentValues.SetValues(settings);
        }
        await db.SaveChangesAsync(ct);
        return settings;
    }
}
