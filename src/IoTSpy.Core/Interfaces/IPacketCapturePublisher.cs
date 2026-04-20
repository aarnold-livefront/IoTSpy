using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IPacketCapturePublisher
{
    Task PublishPacketAsync(CapturedPacket packet, CancellationToken ct = default);

    /// <summary>
    /// Sends a batch of packets to the live capture group in a single SignalR message.
    /// Prefer this over repeated <see cref="PublishPacketAsync"/> calls when emitting
    /// multiple packets at once (PCAP import, Channel consumer flush).
    /// </summary>
    Task PublishPacketBatchAsync(IReadOnlyList<CapturedPacket> packets, CancellationToken ct = default);

    Task PublishStatusAsync(bool isCapturing, Guid? deviceId = null, CancellationToken ct = default);
    Task PublishImportProgressAsync(string jobId, int processed, int total, CancellationToken ct = default);
}
