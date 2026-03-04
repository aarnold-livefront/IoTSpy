using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace IoTSpy.Api.Hubs;

[Authorize]
public class TrafficHub : Hub
{
    // ── Device subscriptions ─────────────────────────────────────────────────
    public async Task SubscribeToDevice(string deviceId) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, $"device:{deviceId}");

    public async Task UnsubscribeFromDevice(string deviceId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"device:{deviceId}");

    // ── Host subscriptions ───────────────────────────────────────────────────
    public async Task SubscribeToHost(string host) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, $"host:{host}");

    public async Task UnsubscribeFromHost(string host) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"host:{host}");

    // ── Method subscriptions ─────────────────────────────────────────────────
    public async Task SubscribeToMethod(string method) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, $"method:{method.ToUpperInvariant()}");

    public async Task UnsubscribeFromMethod(string method) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"method:{method.ToUpperInvariant()}");

    // ── Status code subscriptions ────────────────────────────────────────────
    public async Task SubscribeToStatusCode(int statusCode) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, $"status:{statusCode}");

    public async Task UnsubscribeFromStatusCode(int statusCode) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"status:{statusCode}");

    // ── Protocol subscriptions ───────────────────────────────────────────────
    public async Task SubscribeToProtocol(string protocol) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, $"protocol:{protocol}");

    public async Task UnsubscribeFromProtocol(string protocol) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"protocol:{protocol}");
}

/// <summary>
/// Pushes captured traffic into the SignalR hub. Registered as singleton.
/// Broadcasts to the global stream and to all matching filter groups.
/// </summary>
public class SignalRCapturePublisher(IHubContext<TrafficHub> hub) : ICapturePublisher
{
    public async Task PublishAsync(CapturedRequest capture, CancellationToken ct = default)
    {
        var dto = CaptureToDto(capture);

        // Broadcast to all connected clients (global stream)
        await hub.Clients.All.SendAsync("TrafficCapture", dto, ct);

        // Build the list of matching filter groups
        var groups = new List<string>(6);

        if (capture.DeviceId.HasValue)
            groups.Add($"device:{capture.DeviceId}");

        if (!string.IsNullOrEmpty(capture.Host))
            groups.Add($"host:{capture.Host}");

        if (!string.IsNullOrEmpty(capture.Method))
            groups.Add($"method:{capture.Method.ToUpperInvariant()}");

        if (capture.StatusCode > 0)
            groups.Add($"status:{capture.StatusCode}");

        groups.Add($"protocol:{capture.Protocol}");

        // Send to all matching groups in one call
        if (groups.Count > 0)
            await hub.Clients.Groups(groups).SendAsync("TrafficCapture", dto, ct);
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
