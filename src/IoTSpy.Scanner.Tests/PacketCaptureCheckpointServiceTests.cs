using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using IoTSpy.Scanner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace IoTSpy.Scanner.Tests;

public class PacketCaptureCheckpointServiceTests
{
    private static CapturedPacket MakePacket(long index, string src = "10.0.0.1") =>
        new()
        {
            Id = Guid.NewGuid(),
            CaptureIndex = index,
            Timestamp = DateTimeOffset.UtcNow,
            Protocol = "TCP",
            Layer2Protocol = "Ethernet",
            Layer3Protocol = "IPv4",
            Layer4Protocol = "TCP",
            SourceIp = src,
            DestinationIp = "10.0.0.2",
            SourceMac = string.Empty,
            DestinationMac = string.Empty,
            PayloadPreview = string.Empty,
        };

    private static (PacketCaptureCheckpointService svc, Mock<IPacketRepository> repoMock, LockFreePacketRingBuffer buffer)
        BuildSut(
            long maxIndex = 0,
            IReadOnlyList<CapturedPacket>? recent = null)
    {
        var buffer = new LockFreePacketRingBuffer(100);

        var repoMock = new Mock<IPacketRepository>();
        repoMock
            .Setup(r => r.GetMaxCaptureIndexAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(maxIndex);
        repoMock
            .Setup(r => r.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(recent ?? (IReadOnlyList<CapturedPacket>)Array.Empty<CapturedPacket>());
        repoMock
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<CapturedPacket>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var providerMock = new Mock<IServiceProvider>();
        providerMock
            .Setup(p => p.GetService(typeof(IPacketRepository)))
            .Returns(repoMock.Object);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(providerMock.Object);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        var svc = new PacketCaptureCheckpointService(
            scopeFactoryMock.Object,
            buffer,
            NullLogger<PacketCaptureCheckpointService>.Instance);

        return (svc, repoMock, buffer);
    }

    [Fact]
    public async Task OnStartup_LoadsPersistedPacketsIntoBuffer()
    {
        var persisted = new[] { MakePacket(1), MakePacket(2) };
        var (svc, _, buffer) = BuildSut(maxIndex: 2, recent: persisted);

        using var cts = new CancellationTokenSource();
        var task = svc.StartAsync(cts.Token);
        await Task.Delay(50);
        await cts.CancelAsync();
        try { await task; } catch (OperationCanceledException) { }

        Assert.Equal(2, buffer.Count);
    }

    [Fact]
    public async Task FlushAsync_InsertsNewPackets_AboveWatermark()
    {
        var (svc, repoMock, buffer) = BuildSut(maxIndex: 0);

        buffer.Add(MakePacket(1));
        buffer.Add(MakePacket(2));

        using var cts = new CancellationTokenSource();
        _ = svc.StartAsync(cts.Token);
        await Task.Delay(2_500);
        await cts.CancelAsync();
        await svc.StopAsync(CancellationToken.None);

        repoMock.Verify(
            r => r.AddRangeAsync(
                It.Is<IEnumerable<CapturedPacket>>(pkts => pkts.Count() == 2),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task FlushAsync_SkipsAlreadyFlushedPackets()
    {
        var (svc, repoMock, buffer) = BuildSut(maxIndex: 5);

        buffer.Add(MakePacket(3));
        buffer.Add(MakePacket(4));
        buffer.Add(MakePacket(6));

        using var cts = new CancellationTokenSource();
        _ = svc.StartAsync(cts.Token);
        await Task.Delay(2_500);
        await cts.CancelAsync();
        await svc.StopAsync(CancellationToken.None);

        repoMock.Verify(
            r => r.AddRangeAsync(
                It.Is<IEnumerable<CapturedPacket>>(pkts => pkts.Count() == 1 && pkts.Single().CaptureIndex == 6),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task FlushAsync_ResetsWatermark_WhenCaptureIndexWraps()
    {
        var (svc, repoMock, buffer) = BuildSut(maxIndex: 100);

        buffer.Add(MakePacket(1));
        buffer.Add(MakePacket(2));

        using var cts = new CancellationTokenSource();
        _ = svc.StartAsync(cts.Token);
        await Task.Delay(2_500);
        await cts.CancelAsync();
        await svc.StopAsync(CancellationToken.None);

        repoMock.Verify(
            r => r.AddRangeAsync(
                It.Is<IEnumerable<CapturedPacket>>(pkts => pkts.Count() == 2),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }
}
