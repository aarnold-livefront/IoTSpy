using System.Collections.Concurrent;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;

namespace IoTSpy.Scanner;

/// <summary>
/// Single-writer / multiple-reader ring buffer for live packet capture.
///
/// Thread-safety model:
///   Writer  — the Channel consumer task; calls <see cref="Add"/> sequentially.
///   Readers — any number of query-endpoint threads; call <see cref="Snapshot"/>,
///             <see cref="GetById"/>, etc. concurrently.
///
/// The writer uses plain (non-Interlocked) increments on <see cref="_writePos"/> and
/// <see cref="_totalAdded"/>, both declared <c>volatile</c> so every reader sees the
/// latest value. Array-slot writes precede the <c>_writePos</c> volatile write, which
/// acts as a release fence on the CLR memory model; readers acquire the fence via the
/// subsequent volatile read of <c>_writePos</c>, guaranteeing they see the slot data.
/// Reference-typed array element reads/writes are atomic on all .NET platforms.
/// </summary>
public sealed class LockFreePacketRingBuffer : IPacketBuffer
{
    public const int DefaultCapacity = 10_000;

    private readonly CapturedPacket?[] _ring;
    private volatile int _writePos;    // next slot to write (mod Capacity)
    private volatile int _totalAdded;  // capped at Capacity; readers use this for count

    private readonly ConcurrentDictionary<Guid, byte> _deleted = new();
    private readonly ConcurrentDictionary<Guid, FreezeFrameResult> _freezeFrames = new();

    public LockFreePacketRingBuffer(int capacity = DefaultCapacity)
    {
        Capacity = capacity;
        _ring = new CapturedPacket?[capacity];
    }

    public int Capacity { get; }
    public int Count => _totalAdded;

    public void Add(CapturedPacket packet)
    {
        int slot = _writePos % Capacity;
        _ring[slot] = packet;     // ordinary write — fenced by the volatile write below
        _writePos++;              // volatile write: release fence ensures slot is visible
        if (_totalAdded < Capacity)
            _totalAdded++;        // single writer, no Interlocked needed
    }

    public CapturedPacket[] Snapshot()
    {
        // Capture volatile fields once for a consistent view.
        int pos = _writePos;
        int count = _totalAdded;

        var result = new List<CapturedPacket>(count);
        for (int i = 0; i < count; i++)
        {
            // (pos - count + i) maps i=0 → oldest slot, i=count-1 → newest slot.
            // Adding Capacity*2 keeps the expression positive before the modulo.
            int slot = (pos - count + i + Capacity * 2) % Capacity;
            var pkt = _ring[slot];
            if (pkt is not null && !_deleted.ContainsKey(pkt.Id))
                result.Add(pkt);
        }
        return result.ToArray();
    }

    public CapturedPacket? GetById(Guid id)
    {
        int pos = _writePos;
        int count = _totalAdded;
        // Search newest-first for a fast hit on recently-captured packets.
        for (int i = count - 1; i >= 0; i--)
        {
            int slot = (pos - count + i + Capacity * 2) % Capacity;
            var pkt = _ring[slot];
            if (pkt?.Id == id)
                return _deleted.ContainsKey(id) ? null : pkt;
        }
        return null;
    }

    public bool TryDelete(Guid id)
    {
        _deleted.TryAdd(id, 0);
        _freezeFrames.TryRemove(id, out _);
        return true;
    }

    public void Clear()
    {
        // Not strictly race-free against a concurrent Add(), but Clear() is
        // an explicit UI action and approximate semantics are acceptable here.
        Array.Clear(_ring);
        _writePos = 0;
        _totalAdded = 0;
        _deleted.Clear();
        _freezeFrames.Clear();
    }

    public void SetFreezeFrame(Guid packetId, FreezeFrameResult frame) =>
        _freezeFrames[packetId] = frame;

    public FreezeFrameResult? GetFreezeFrame(Guid packetId)
    {
        _freezeFrames.TryGetValue(packetId, out var frame);
        return frame;
    }

    public bool TryDeleteFreezeFrame(Guid packetId) =>
        _freezeFrames.TryRemove(packetId, out _);
}
