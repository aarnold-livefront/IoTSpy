using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface ICoapProxy
{
    bool IsRunning { get; }
    long MessagesProxied { get; }
    Task StartAsync(CoapProxySettings settings, CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}
