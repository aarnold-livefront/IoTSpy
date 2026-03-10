using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace IoTSpy.Api.Hubs;

/// <summary>
/// SignalR hub that streams live packet captures to connected clients.
/// </summary>
[Authorize]
public class PacketCaptureHub : Hub
{
    private const string LiveGroup = "packet-capture-live";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, LiveGroup);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, LiveGroup);
        await base.OnDisconnectedAsync(exception);
    }
}
