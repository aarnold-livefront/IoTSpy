using IoTSpy.Api.Hubs;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.SignalR;

namespace IoTSpy.Api.Services;

public class SignalRPacketPublisher(IHubContext<PacketCaptureHub> hub) : IPacketCapturePublisher
{
    private const string LiveGroup = "packet-capture-live";

    public Task PublishPacketAsync(CapturedPacket packet, CancellationToken ct = default)
        => hub.Clients.Group(LiveGroup).SendAsync("PacketCaptured", new
        {
            id = packet.Id,
            timestamp = packet.Timestamp,
            protocol = packet.Protocol,
            sourceIp = packet.SourceIp,
            destinationIp = packet.DestinationIp,
            sourcePort = packet.SourcePort,
            destinationPort = packet.DestinationPort,
            length = packet.Length,
            payloadPreview = packet.PayloadPreview,
            tcpFlags = packet.TcpFlags,
            isError = packet.IsError,
            isRetransmission = packet.IsRetransmission
        }, ct);

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
