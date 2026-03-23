using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IMqttBrokerProxy
{
    bool IsRunning { get; }
    int ActiveConnections { get; }
    Task StartAsync(MqttBrokerSettings settings, CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}
