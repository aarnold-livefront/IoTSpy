using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

/// <summary>
/// Broadcasts captured traffic to real-time consumers (SignalR hub, etc.).
/// </summary>
public interface ICapturePublisher
{
    Task PublishAsync(CapturedRequest capture, CancellationToken ct = default);
}
