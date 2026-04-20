using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

/// <summary>
/// Lock-free, single-writer / multiple-reader ring buffer for live captured packets.
/// The single writer is the Channel consumer in <c>PacketCaptureService</c>;
/// query endpoints are the concurrent readers.
/// </summary>
public interface IPacketBuffer
{
    int Capacity { get; }
    int Count { get; }

    /// <summary>Called only from the single Channel consumer thread.</summary>
    void Add(CapturedPacket packet);

    /// <summary>
    /// Atomic snapshot ordered oldest-to-newest, excluding soft-deleted entries.
    /// Safe to call from any thread.
    /// </summary>
    CapturedPacket[] Snapshot();

    /// <summary>O(n) scan; prefer <see cref="Snapshot"/> for bulk access.</summary>
    CapturedPacket? GetById(Guid id);

    /// <summary>Soft-deletes the packet so it is excluded from future snapshots.</summary>
    bool TryDelete(Guid id);

    void Clear();

    void SetFreezeFrame(Guid packetId, FreezeFrameResult frame);
    FreezeFrameResult? GetFreezeFrame(Guid packetId);
    bool TryDeleteFreezeFrame(Guid packetId);
}
