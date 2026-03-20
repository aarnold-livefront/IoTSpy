using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

/// <summary>
/// Publishes anomaly alerts to connected real-time subscribers (e.g., SignalR).
/// </summary>
public interface IAnomalyAlertPublisher
{
    Task PublishAsync(AnomalyAlert alert, CancellationToken ct = default);
}
