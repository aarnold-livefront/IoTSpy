using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IProxyService
{
    bool IsRunning { get; }
    int Port { get; }
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    ProxySettings GetSettings();
    Task UpdateSettingsAsync(ProxySettings settings, CancellationToken ct = default);
}
