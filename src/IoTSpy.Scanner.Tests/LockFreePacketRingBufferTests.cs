using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using IoTSpy.Scanner;
using Xunit;

namespace IoTSpy.Scanner.Tests;

public class LockFreePacketRingBufferTests
{
    private static CapturedPacket MakePacket(string protocol = "TCP", string src = "10.0.0.1") =>
        new() { Id = Guid.NewGuid(), Protocol = protocol, SourceIp = src, Timestamp = DateTimeOffset.UtcNow };

    [Fact]
    public void Add_IncrementsCount()
    {
        var buf = new LockFreePacketRingBuffer(10);
        buf.Add(MakePacket());
        Assert.Equal(1, buf.Count);
    }

    [Fact]
    public void Add_CapsCountAtCapacity()
    {
        var buf = new LockFreePacketRingBuffer(5);
        for (var i = 0; i < 10; i++) buf.Add(MakePacket());
        Assert.Equal(5, buf.Count);
    }

    [Fact]
    public void Snapshot_ReturnsAllPackets_WhenNotFull()
    {
        var buf = new LockFreePacketRingBuffer(10);
        buf.Add(MakePacket("TCP"));
        buf.Add(MakePacket("UDP"));
        var snap = buf.Snapshot();
        Assert.Equal(2, snap.Length);
    }

    [Fact]
    public void Snapshot_EvictsOldest_WhenFull()
    {
        var buf = new LockFreePacketRingBuffer(3);
        var first = MakePacket("TCP");
        buf.Add(first);
        buf.Add(MakePacket("UDP"));
        buf.Add(MakePacket("DNS"));
        buf.Add(MakePacket("ARP")); // evicts 'first'

        var snap = buf.Snapshot();
        Assert.Equal(3, snap.Length);
        Assert.DoesNotContain(snap, p => p.Id == first.Id);
    }

    [Fact]
    public void Snapshot_OrderedOldestToNewest()
    {
        var buf = new LockFreePacketRingBuffer(10);
        var p1 = MakePacket("A");
        var p2 = MakePacket("B");
        var p3 = MakePacket("C");
        buf.Add(p1); buf.Add(p2); buf.Add(p3);

        var snap = buf.Snapshot();
        Assert.Equal(p1.Id, snap[0].Id);
        Assert.Equal(p3.Id, snap[2].Id);
    }

    [Fact]
    public void GetById_ReturnsPacket_WhenPresent()
    {
        var buf = new LockFreePacketRingBuffer(10);
        var pkt = MakePacket();
        buf.Add(pkt);
        var found = buf.GetById(pkt.Id);
        Assert.NotNull(found);
        Assert.Equal(pkt.Id, found.Id);
    }

    [Fact]
    public void GetById_ReturnsNull_WhenAbsent()
    {
        var buf = new LockFreePacketRingBuffer(10);
        buf.Add(MakePacket());
        Assert.Null(buf.GetById(Guid.NewGuid()));
    }

    [Fact]
    public void TryDelete_ExcludesPacketFromSnapshot()
    {
        var buf = new LockFreePacketRingBuffer(10);
        var pkt = MakePacket();
        buf.Add(pkt);

        Assert.True(buf.TryDelete(pkt.Id));
        Assert.Empty(buf.Snapshot());
        Assert.Null(buf.GetById(pkt.Id));
    }

    [Fact]
    public void TryDelete_ReturnsFalse_ForAbsentId()
    {
        var buf = new LockFreePacketRingBuffer(10);
        buf.Add(MakePacket());

        Assert.False(buf.TryDelete(Guid.NewGuid()));
    }

    [Fact]
    public void Clear_ResetsCountAndSnapshot()
    {
        var buf = new LockFreePacketRingBuffer(10);
        buf.Add(MakePacket());
        buf.Add(MakePacket());

        buf.Clear();

        Assert.Equal(0, buf.Count);
        Assert.Empty(buf.Snapshot());
    }

    [Fact]
    public void FreezeFrame_RoundTrip()
    {
        var buf = new LockFreePacketRingBuffer(10);
        var id = Guid.NewGuid();
        var frame = new FreezeFrameResult { PacketId = id, HexDump = "AABB" };

        buf.SetFreezeFrame(id, frame);
        var retrieved = buf.GetFreezeFrame(id);

        Assert.NotNull(retrieved);
        Assert.Equal("AABB", retrieved.HexDump);
    }

    [Fact]
    public void TryDeleteFreezeFrame_RemovesFrame()
    {
        var buf = new LockFreePacketRingBuffer(10);
        var id = Guid.NewGuid();
        buf.SetFreezeFrame(id, new FreezeFrameResult { PacketId = id });

        buf.TryDeleteFreezeFrame(id);

        Assert.Null(buf.GetFreezeFrame(id));
    }

    [Fact]
    public void Snapshot_ReturnsEmpty_WhenNothingAdded()
    {
        var buf = new LockFreePacketRingBuffer(10);
        Assert.Empty(buf.Snapshot());
    }

    [Fact]
    public void Snapshot_ConsistentAfterWrap()
    {
        // Capacity = 4, add 7 packets; only the last 4 should appear.
        var buf = new LockFreePacketRingBuffer(4);
        var packets = Enumerable.Range(0, 7).Select(_ => MakePacket()).ToArray();
        foreach (var p in packets) buf.Add(p);

        var snap = buf.Snapshot();
        Assert.Equal(4, snap.Length);
        // Last 4 packets (index 3-6) should be in the snapshot.
        var snapIds = snap.Select(p => p.Id).ToHashSet();
        for (int i = 3; i < 7; i++)
            Assert.Contains(packets[i].Id, snapIds);
    }
}
