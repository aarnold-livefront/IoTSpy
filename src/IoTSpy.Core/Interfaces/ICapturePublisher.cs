using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

/// <summary>
/// Broadcasts captured traffic to real-time consumers (SignalR hub, etc.).
/// </summary>
public interface ICapturePublisher
{
    Task PublishAsync(CapturedRequest capture, CancellationToken ct = default);
    Task PublishWebSocketFrameAsync(WebSocketFrame frame, CancellationToken ct = default);
    Task PublishMqttMessageAsync(MqttCapturedMessage message, CancellationToken ct = default);
}
