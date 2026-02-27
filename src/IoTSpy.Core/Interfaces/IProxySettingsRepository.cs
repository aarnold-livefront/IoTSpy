using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IProxySettingsRepository
{
    Task<ProxySettings> GetAsync(CancellationToken ct = default);
    Task<ProxySettings> SaveAsync(ProxySettings settings, CancellationToken ct = default);
}
