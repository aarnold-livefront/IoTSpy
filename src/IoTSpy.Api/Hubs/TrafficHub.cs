using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace IoTSpy.Api.Hubs;

[Authorize]
public class TrafficHub : Hub
{
    // Clients subscribe to specific device streams or the global stream
    public async Task SubscribeToDevice(string deviceId) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, $"device:{deviceId}");

    public async Task UnsubscribeFromDevice(string deviceId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"device:{deviceId}");
}

/// <summary>
/// Pushes captured traffic into the SignalR hub. Registered as singleton.
/// </summary>
public class SignalRCapturePublisher(IHubContext<TrafficHub> hub) : ICapturePublisher
{
    public async Task PublishAsync(CapturedRequest capture, CancellationToken ct = default)
    {
        var dto = CaptureToDto(capture);

        // Broadcast to all connected clients (global stream)
        await hub.Clients.All.SendAsync("TrafficCapture", dto, ct);

        // Also broadcast to device-specific group
        if (capture.DeviceId.HasValue)
            await hub.Clients.Group($"device:{capture.DeviceId}").SendAsync("TrafficCapture", dto, ct);
    }

    private static object CaptureToDto(CapturedRequest c) => new
    {
        id = c.Id,
        deviceId = c.DeviceId,
        method = c.Method,
        scheme = c.Scheme,
        host = c.Host,
        port = c.Port,
        path = c.Path,
        query = c.Query,
        statusCode = c.StatusCode,
        statusMessage = c.StatusMessage,
        protocol = c.Protocol.ToString(),
        isTls = c.IsTls,
        tlsVersion = c.TlsVersion,
        timestamp = c.Timestamp,
        durationMs = c.DurationMs,
        clientIp = c.ClientIp,
        requestBodySize = c.RequestBodySize,
        responseBodySize = c.ResponseBodySize
    };
}
