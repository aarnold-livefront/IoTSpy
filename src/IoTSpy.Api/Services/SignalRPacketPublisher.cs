using IoTSpy.Api.Hubs;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.SignalR;

namespace IoTSpy.Api.Services;

public class SignalRPacketPublisher(IHubContext<PacketCaptureHub> hub) : IPacketCapturePublisher
{
    private const string LiveGroup = "packet-capture-live";

    public Task PublishPacketAsync(CapturedPacket packet, CancellationToken ct = default)
        => hub.Clients.Group(LiveGroup).SendAsync("PacketCaptured", ToDto(packet), ct);

    public Task PublishPacketBatchAsync(IReadOnlyList<CapturedPacket> packets, CancellationToken ct = default)
        => hub.Clients.Group(LiveGroup).SendAsync("PacketCapturedBatch", packets.Select(ToDto).ToArray(), ct);

    private static object ToDto(CapturedPacket p) => new
    {
        id = p.Id,
        timestamp = p.Timestamp,
        protocol = p.Protocol,
        sourceIp = p.SourceIp,
        destinationIp = p.DestinationIp,
        sourcePort = p.SourcePort,
        destinationPort = p.DestinationPort,
        length = p.Length,
        payloadPreview = p.PayloadPreview,
        tcpFlags = p.TcpFlags,
        isError = p.IsError,
        isRetransmission = p.IsRetransmission
    };

    public Task PublishStatusAsync(bool isCapturing, Guid? deviceId = null, CancellationToken ct = default)
        => hub.Clients.All.SendAsync("CaptureStatus", new { isCapturing, deviceId }, ct);

    public Task PublishImportProgressAsync(string jobId, int processed, int total, CancellationToken ct = default)
        => hub.Clients.Group(LiveGroup).SendAsync("ImportProgress", new
        {
            jobId,
            processed,
            total,
            percent = total > 0 ? (int)((double)processed / total * 100) : 0
        }, ct);
}
