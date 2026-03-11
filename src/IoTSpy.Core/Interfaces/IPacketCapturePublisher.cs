using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IPacketCapturePublisher
{
    Task PublishPacketAsync(CapturedPacket packet, CancellationToken ct = default);
    Task PublishStatusAsync(bool isCapturing, Guid? deviceId = null, CancellationToken ct = default);
}
