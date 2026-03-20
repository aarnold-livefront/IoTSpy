using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using System.Text;

namespace IoTSpy.Scanner;

/// <summary>
/// Implements live packet capture via SharpPcap/libpcap and serves as the
/// primary implementation of <see cref="IPacketCaptureService"/>.
/// Packets are held in an in-memory circular buffer and optionally streamed
/// to connected clients via <see cref="IPacketCapturePublisher"/>.
/// </summary>
public sealed class PacketCaptureService : IPacketCaptureService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPacketCapturePublisher _publisher;
    private readonly ILogger<PacketCaptureService> _logger;

    private ILiveDevice? _activeDevice;
    private Guid _activeDeviceId;
    private long _captureIndex;
    private bool _devicesEnumerated;

    // Ring buffer backed by a fixed-capacity queue — O(1) enqueue/dequeue
    private readonly object _lock = new();
    private readonly LinkedList<CapturedPacket> _livePackets = new();
    private readonly Dictionary<Guid, FreezeFrameResult> _freezeFrames = new();

    private const int MaxLivePackets = 10_000;

    public bool IsCaptureActive { get; private set; }

    public PacketCaptureService(
        IServiceScopeFactory scopeFactory,
        IPacketCapturePublisher publisher,
        ILogger<PacketCaptureService> logger)
    {
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _logger = logger;
    }

    // ── Interface enumeration ────────────────────────────────────────────────

    public async Task<IEnumerable<NetworkDevice>> ListInterfacesAsync()
    {
        var result = new List<NetworkDevice>();
        try
        {
            var pcapDevices = CaptureDeviceList.Instance;
            foreach (var dev in pcapDevices)
            {
                var nd = new NetworkDevice
                {
                    Id = Guid.NewGuid(),
                    Name = dev.Name,
                    Description = dev is LibPcapLiveDevice ld ? ld.Description : dev.Name,
                    MacAddress = dev is LibPcapLiveDevice lm
                        ? lm.MacAddress?.ToString() ?? string.Empty
                        : string.Empty,
                    IpAddress = dev is LibPcapLiveDevice la
                        ? la.Addresses.FirstOrDefault(a => a.Addr?.ipAddress != null)
                              ?.Addr?.ipAddress?.ToString() ?? string.Empty
                        : string.Empty,
                    SupportsPromiscuousMode = true,
                    IsActive = true
                };
                result.Add(nd);
            }
            await SyncDevicesToDbAsync(result);
            _devicesEnumerated = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate network interfaces via SharpPcap");
        }
        return result;
    }

    /// <summary>
    /// Ensures device list has been synced to DB at least once.
    /// Called lazily before operations that depend on persisted devices.
    /// </summary>
    private async Task EnsureDevicesEnumeratedAsync()
    {
        if (!_devicesEnumerated)
            await ListInterfacesAsync();
    }

    public async Task<NetworkDevice?> GetByIdAsync(Guid id)
    {
        await EnsureDevicesEnumeratedAsync();
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ICaptureDeviceRepository>();
        var dev = await repo.GetByIdAsync(id);
        if (dev == null) return null;
        return new NetworkDevice
        {
            Id = dev.Id,
            Name = dev.Name,
            Description = dev.DisplayName,
            IpAddress = dev.IpAddress,
            MacAddress = dev.MacAddress
        };
    }

    private async Task SyncDevicesToDbAsync(List<NetworkDevice> discovered)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ICaptureDeviceRepository>();
        var existingDevices = (await repo.GetAllAsync()).ToList();

        foreach (var nd in discovered)
        {
            var existing = existingDevices.FirstOrDefault(d => d.Name == nd.Name);
            if (existing == null)
            {
                await repo.AddAsync(new CaptureDevice
                {
                    Id = Guid.NewGuid(),
                    Name = nd.Name,
                    DisplayName = nd.Description,
                    IpAddress = nd.IpAddress,
                    MacAddress = nd.MacAddress,
                    CanCapture = true,
                    SupportsPromiscuousMode = nd.SupportsPromiscuousMode
                });
            }
            else if (existing.IpAddress != nd.IpAddress || existing.MacAddress != nd.MacAddress)
            {
                // Update stale address info
                existing.IpAddress = nd.IpAddress;
                existing.MacAddress = nd.MacAddress;
                await repo.UpdateAsync(existing);
            }
        }
    }

    // ── Capture lifecycle ────────────────────────────────────────────────────

    public async Task<bool> SelectInterfaceForCaptureAsync(Guid deviceId, CancellationToken ct = default)
    {
        await EnsureDevicesEnumeratedAsync();
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ICaptureDeviceRepository>();
        var dev = await repo.GetByIdAsync(deviceId);
        if (dev == null) return false;

        dev.CanCapture = true;
        await repo.UpdateAsync(dev);
        return true;
    }

    public Task<bool> DeselectCurrentInterfaceAsync()
    {
        _activeDeviceId = Guid.Empty;
        return Task.FromResult(true);
    }

    public async Task<bool> StartCaptureAsync(Guid deviceId, CancellationToken ct = default)
    {
        if (IsCaptureActive) await StopCaptureAsync();

        await EnsureDevicesEnumeratedAsync();

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ICaptureDeviceRepository>();
        var captureDevice = await repo.GetByIdAsync(deviceId);
        if (captureDevice == null)
        {
            _logger.LogWarning("CaptureDevice {Id} not found", deviceId);
            return false;
        }

        try
        {
            var pcapDevice = CaptureDeviceList.Instance
                .OfType<LibPcapLiveDevice>()
                .FirstOrDefault(d => d.Name == captureDevice.Name);

            if (pcapDevice == null)
            {
                _logger.LogWarning("SharpPcap device '{Name}' not found", captureDevice.Name);
                return false;
            }

            lock (_lock)
            {
                _livePackets.Clear();
                _freezeFrames.Clear();
                _captureIndex = 0;
                _activeDeviceId = deviceId;
            }

            _activeDevice = pcapDevice;
            _activeDevice.OnPacketArrival += OnPacketArrival;
            _activeDevice.Open(DeviceModes.Promiscuous, 1000);
            _activeDevice.StartCapture();

            IsCaptureActive = true;
            await _publisher.PublishStatusAsync(true, deviceId, ct);
            _logger.LogInformation("Packet capture started on {Device}", captureDevice.Name);
            return true;
        }
        catch (Exception ex)
        {
            IsCaptureActive = false;
            _logger.LogError(ex, "Failed to start packet capture on {Device}", captureDevice.Name);
            return false;
        }
    }

    public async Task<bool> StopCaptureAsync()
    {
        if (!IsCaptureActive) return false;

        try
        {
            if (_activeDevice != null)
            {
                _activeDevice.StopCapture();
                _activeDevice.OnPacketArrival -= OnPacketArrival;
                _activeDevice.Close();
                _activeDevice = null;
            }
            IsCaptureActive = false;
            await _publisher.PublishStatusAsync(false, _activeDeviceId);
            _logger.LogInformation("Packet capture stopped");
            return true;
        }
        catch (Exception ex)
        {
            IsCaptureActive = false;
            _logger.LogError(ex, "Error stopping packet capture");
            return false;
        }
    }

    // ── Packet arrival (called on SharpPcap capture thread) ──────────────────

    private void OnPacketArrival(object sender, PacketCapture e)
    {
        try
        {
            var rawCapture = e.GetPacket();
            var packet = ParseRawPacket(rawCapture);

            lock (_lock)
            {
                _livePackets.AddLast(packet);
                if (_livePackets.Count > MaxLivePackets)
                    _livePackets.RemoveFirst(); // O(1) for LinkedList
            }

            // Fire-and-forget publish — observe exceptions to prevent
            // unobserved task exceptions from crashing the process.
            _ = PublishSafeAsync(packet);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error processing captured packet");
        }
    }

    private async Task PublishSafeAsync(CapturedPacket packet)
    {
        try
        {
            await _publisher.PublishPacketAsync(packet);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to publish captured packet via SignalR");
        }
    }

    private CapturedPacket ParseRawPacket(RawCapture rawCapture)
    {
        var index = Interlocked.Increment(ref _captureIndex);
        var packet = new CapturedPacket
        {
            Id = Guid.NewGuid(),
            CaptureIndex = index,
            Timestamp = DateTimeOffset.UtcNow,
            DeviceId = _activeDeviceId,
            Length = rawCapture.Data.Length,
            RawData = rawCapture.Data, // stored in-memory only ([NotMapped])
            Protocol = "Unknown",
            Layer2Protocol = "Ethernet",
            Layer3Protocol = "Unknown",
            Layer4Protocol = "Unknown",
        };

        try
        {
            var parsed = Packet.ParsePacket(rawCapture.LinkLayerType, rawCapture.Data);
            if (parsed == null) return packet;

            // Ethernet
            var eth = parsed.Extract<EthernetPacket>();
            if (eth != null)
            {
                packet.Layer2Protocol = "Ethernet";
                packet.SourceMac = eth.SourceHardwareAddress?.ToString() ?? string.Empty;
                packet.DestinationMac = eth.DestinationHardwareAddress?.ToString() ?? string.Empty;
            }

            // ARP — layer 3, no transport layer
            var arp = parsed.Extract<ArpPacket>();
            if (arp != null)
            {
                packet.Protocol = "ARP";
                packet.Layer3Protocol = "ARP";
                packet.Layer4Protocol = "N/A";
                packet.ArpOperation = arp.Operation.ToString();
                packet.ArpSenderMac = arp.SenderHardwareAddress?.ToString();
                packet.ArpTargetIp = arp.TargetProtocolAddress?.ToString();
                packet.SourceIp = arp.SenderProtocolAddress?.ToString() ?? string.Empty;
                packet.DestinationIp = arp.TargetProtocolAddress?.ToString() ?? string.Empty;
                return packet;
            }

            // IPv4
            var ip4 = parsed.Extract<IPv4Packet>();
            if (ip4 != null)
            {
                packet.Layer3Protocol = "IPv4";
                packet.SourceIp = ip4.SourceAddress.ToString();
                packet.DestinationIp = ip4.DestinationAddress.ToString();
                packet.IsFragment = ip4.FragmentOffset != 0;
            }

            // IPv6
            var ip6 = parsed.Extract<IPv6Packet>();
            if (ip6 != null)
            {
                packet.Layer3Protocol = "IPv6";
                packet.SourceIp = ip6.SourceAddress.ToString();
                packet.DestinationIp = ip6.DestinationAddress.ToString();
            }

            // TCP
            var tcp = parsed.Extract<TcpPacket>();
            if (tcp != null)
            {
                packet.Layer4Protocol = "TCP";
                packet.SourcePort = tcp.SourcePort;
                packet.DestinationPort = tcp.DestinationPort;
                packet.TcpFlags = BuildTcpFlags(tcp);
                packet.Protocol = ResolveAppProtocol(tcp.SourcePort, tcp.DestinationPort) ?? "TCP";

                if (tcp.PayloadData?.Length > 0)
                    packet.PayloadPreview = GetPayloadPreview(tcp.PayloadData);

                return packet;
            }

            // UDP
            var udp = parsed.Extract<UdpPacket>();
            if (udp != null)
            {
                packet.Layer4Protocol = "UDP";
                packet.SourcePort = udp.SourcePort;
                packet.DestinationPort = udp.DestinationPort;
                packet.UdpLength = udp.Length.ToString();
                packet.Protocol = ResolveAppProtocol(udp.SourcePort, udp.DestinationPort) ?? "UDP";

                if (udp.PayloadData?.Length > 0)
                    packet.PayloadPreview = GetPayloadPreview(udp.PayloadData);

                return packet;
            }

            // ICMP
            var icmp = parsed.Extract<IcmpV4Packet>();
            if (icmp != null)
            {
                packet.Protocol = "ICMP";
                packet.Layer4Protocol = "ICMP";
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Packet parse error (index {Index})", index);
        }

        return packet;
    }

    private static string BuildTcpFlags(TcpPacket tcp)
    {
        var flags = new List<string>(6);
        if (tcp.Synchronize) flags.Add("SYN");
        if (tcp.Acknowledgment) flags.Add("ACK");
        if (tcp.Finished) flags.Add("FIN");
        if (tcp.Reset) flags.Add("RST");
        if (tcp.Push) flags.Add("PSH");
        if (tcp.Urgent) flags.Add("URG");
        return string.Join("|", flags);
    }

    private static string? ResolveAppProtocol(int srcPort, int dstPort)
    {
        // Check both directions — the well-known port is usually the lower one
        return MatchPort(srcPort) ?? MatchPort(dstPort);

        static string? MatchPort(int port) => port switch
        {
            80 => "HTTP",
            443 => "HTTPS",
            53 => "DNS",
            1883 => "MQTT",
            8883 => "MQTT/TLS",
            5353 => "mDNS",
            5683 => "CoAP",
            67 or 68 => "DHCP",
            22 => "SSH",
            23 => "Telnet",
            _ => null
        };
    }

    private static string GetPayloadPreview(byte[] data)
    {
        var preview = data.AsSpan(0, Math.Min(data.Length, 128));
        var sb = new StringBuilder(preview.Length);
        foreach (var b in preview)
            sb.Append(b >= 0x20 && b < 0x7F ? (char)b : '.');
        return sb.ToString();
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    public Task<NetworkDeviceStatistics?> GetLiveStatsAsync(Guid deviceId)
    {
        if (_activeDeviceId != deviceId)
            return Task.FromResult<NetworkDeviceStatistics?>(null);

        lock (_lock)
        {
            return Task.FromResult<NetworkDeviceStatistics?>(new NetworkDeviceStatistics
            {
                DeviceId = deviceId,
                TotalPacketsCaptured = Interlocked.Read(ref _captureIndex),
                PacketsPerSecond = 0,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
    }

    public Task<PagedResult<CapturedPacket>> GetCapturedPacketsAsync(
        PacketFilter filter,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        List<CapturedPacket> filtered;
        lock (_lock)
            filtered = ApplyFilter(_livePackets, filter);

        var total = filtered.Count;
        var items = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult(new PagedResult<CapturedPacket>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public Task<IEnumerable<CapturedPacket>> FilterPacketsAsync(
        PacketFilterDto filter,
        CancellationToken ct = default)
    {
        var pf = new PacketFilter
        {
            Protocol = filter.Protocol,
            SourceIp = filter.SourceIp,
            DestinationIp = filter.DestinationIp,
            SourcePort = filter.SourcePort,
            DestinationPort = filter.DestinationPort,
            MacAddress = filter.MacAddress,
            ShowOnlyErrors = filter.ShowOnlyErrors,
            ShowOnlyRetransmissions = filter.ShowOnlyRetransmissions,
            FromTime = filter.FromTime.HasValue
                ? new DateTimeOffset(filter.FromTime.Value, TimeSpan.Zero)
                : null,
            ToTime = filter.ToTime.HasValue
                ? new DateTimeOffset(filter.ToTime.Value, TimeSpan.Zero)
                : null,
            PayloadSearch = filter.PayloadSearch
        };

        List<CapturedPacket> result;
        lock (_lock)
            result = ApplyFilter(_livePackets, pf);

        // Materialize inside lock, then limit outside
        return Task.FromResult<IEnumerable<CapturedPacket>>(
            result.Take(filter.Limit).ToList());
    }

    private static List<CapturedPacket> ApplyFilter(
        IEnumerable<CapturedPacket> source, PacketFilter f)
    {
        var q = source.AsEnumerable();

        if (!string.IsNullOrEmpty(f.Protocol))
            q = q.Where(p => p.Protocol.Contains(f.Protocol!, StringComparison.OrdinalIgnoreCase)
                           || p.Layer4Protocol.Contains(f.Protocol!, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(f.SourceIp))
            q = q.Where(p => p.SourceIp.Contains(f.SourceIp!));

        if (!string.IsNullOrEmpty(f.DestinationIp))
            q = q.Where(p => p.DestinationIp.Contains(f.DestinationIp!));

        if (f.SourcePort.HasValue)
            q = q.Where(p => p.SourcePort == f.SourcePort);

        if (f.DestinationPort.HasValue)
            q = q.Where(p => p.DestinationPort == f.DestinationPort);

        if (!string.IsNullOrEmpty(f.MacAddress))
            q = q.Where(p => p.SourceMac.Equals(f.MacAddress, StringComparison.OrdinalIgnoreCase)
                           || p.DestinationMac.Equals(f.MacAddress, StringComparison.OrdinalIgnoreCase));

        if (f.ShowOnlyErrors)
            q = q.Where(p => p.IsError);

        if (f.ShowOnlyRetransmissions)
            q = q.Where(p => p.IsRetransmission);

        if (f.FromTime.HasValue)
            q = q.Where(p => p.Timestamp >= f.FromTime.Value);

        if (f.ToTime.HasValue)
            q = q.Where(p => p.Timestamp <= f.ToTime.Value);

        if (!string.IsNullOrEmpty(f.PayloadSearch))
            q = q.Where(p => p.PayloadPreview.Contains(f.PayloadSearch!, StringComparison.OrdinalIgnoreCase));

        return q.OrderByDescending(p => p.Timestamp).ToList();
    }

    public Task<CapturedPacket?> GetPacketByIdAsync(Guid id, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var packet = _livePackets.FirstOrDefault(p => p.Id == id);
            return Task.FromResult(packet);
        }
    }

    public Task<FreezeFrameResult?> FreezeFrameAsync(Guid id, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var packet = _livePackets.FirstOrDefault(p => p.Id == id);
            if (packet == null) return Task.FromResult<FreezeFrameResult?>(null);

            var result = BuildFreezeFrame(packet);
            _freezeFrames[id] = result;
            return Task.FromResult<FreezeFrameResult?>(result);
        }
    }

    public Task<FreezeFrameResult?> GetFreezeFrameAsync(Guid id, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _freezeFrames.TryGetValue(id, out var frame);
            return Task.FromResult(frame);
        }
    }

    private static FreezeFrameResult BuildFreezeFrame(CapturedPacket packet)
    {
        // Use raw frame data for the hex dump when available, otherwise fall back to preview
        var rawBytes = packet.RawData ?? Encoding.UTF8.GetBytes(packet.PayloadPreview);
        var hexDump = FormatHexDump(rawBytes);

        return new FreezeFrameResult
        {
            PacketId = packet.Id,
            Timestamp = packet.Timestamp,
            FullPayloadHex = BitConverter.ToString(rawBytes).Replace("-", " "),
            HexDump = hexDump,
            ProtocolDetails = $"{packet.Protocol} ({packet.Layer4Protocol})",
            Layer2Info = $"{packet.Layer2Protocol} src={packet.SourceMac} dst={packet.DestinationMac}",
            Layer3Info = $"{packet.Layer3Protocol} src={packet.SourceIp} dst={packet.DestinationIp}",
            Layer4Info = packet.TcpFlags != null
                ? $"TCP src={packet.SourcePort} dst={packet.DestinationPort} flags={packet.TcpFlags}"
                : $"{packet.Layer4Protocol} src={packet.SourcePort} dst={packet.DestinationPort}"
        };
    }

    /// <summary>
    /// Formats raw bytes as a Wireshark-style hex dump (offset | hex | ASCII).
    /// </summary>
    private static string FormatHexDump(byte[] data)
    {
        var sb = new StringBuilder();
        for (int offset = 0; offset < data.Length; offset += 16)
        {
            sb.Append($"{offset:X8}  ");
            int lineLen = Math.Min(16, data.Length - offset);
            for (int i = 0; i < 16; i++)
            {
                sb.Append(i < lineLen ? $"{data[offset + i]:X2} " : "   ");
                if (i == 7) sb.Append(' ');
            }
            sb.Append(" |");
            for (int i = 0; i < lineLen; i++)
            {
                var b = data[offset + i];
                sb.Append(b >= 0x20 && b < 0x7F ? (char)b : '.');
            }
            sb.AppendLine("|");
        }
        return sb.ToString();
    }

    public Task<bool> DeletePacketAsync(Guid id, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var node = _livePackets.First;
            while (node != null)
            {
                if (node.Value.Id == id)
                {
                    _livePackets.Remove(node);
                    _freezeFrames.Remove(id);
                    return Task.FromResult(true);
                }
                node = node.Next;
            }
            return Task.FromResult(false);
        }
    }

    public Task<bool> ClearCapturesAsync()
    {
        lock (_lock)
        {
            _livePackets.Clear();
            _freezeFrames.Clear();
        }
        return Task.FromResult(true);
    }

    // ── PCAP export ──────────────────────────────────────────────────────────

    public Task<byte[]?> ExportToPcapAsync(CancellationToken ct = default)
    {
        List<CapturedPacket> snapshot;
        lock (_lock)
            snapshot = _livePackets.ToList();

        if (snapshot.Count == 0)
            return Task.FromResult<byte[]?>(null);

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // PCAP global header (libpcap format)
        bw.Write(0xa1b2c3d4u);  // magic number
        bw.Write((ushort)2);     // version major
        bw.Write((ushort)4);     // version minor
        bw.Write(0);             // thiszone
        bw.Write(0u);            // sigfigs
        bw.Write(65535u);        // snaplen
        bw.Write(1u);            // network = LINKTYPE_ETHERNET

        foreach (var p in snapshot)
        {
            // Use raw frame data when available; skip packets without it
            var frameData = p.RawData;
            if (frameData == null || frameData.Length == 0)
                continue;

            var tsSec = (uint)p.Timestamp.ToUnixTimeSeconds();
            var tsUsec = (uint)(p.Timestamp.Millisecond * 1000);
            bw.Write(tsSec);
            bw.Write(tsUsec);
            bw.Write((uint)frameData.Length);  // incl_len
            bw.Write((uint)frameData.Length);  // orig_len
            bw.Write(frameData);
        }

        return Task.FromResult<byte[]?>(ms.ToArray());
    }

    public async Task<byte[]?> ExportToPcapFilteredAsync(PacketFilterDto filter, CancellationToken ct = default)
    {
        var filtered = (await FilterPacketsAsync(filter, ct)).ToList();
        if (filtered.Count == 0) return null;

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write(0xa1b2c3d4u);
        bw.Write((ushort)2);
        bw.Write((ushort)4);
        bw.Write(0);
        bw.Write(0u);
        bw.Write(65535u);
        bw.Write(1u);

        // Get raw data from live buffer for these packet IDs
        Dictionary<Guid, byte[]?> rawDataById;
        lock (_lock)
            rawDataById = _livePackets.ToDictionary(p => p.Id, p => p.RawData);

        foreach (var p in filtered)
        {
            if (!rawDataById.TryGetValue(p.Id, out var frameData) || frameData is null || frameData.Length == 0)
                continue;

            var tsSec = (uint)p.Timestamp.ToUnixTimeSeconds();
            var tsUsec = (uint)(p.Timestamp.Millisecond * 1000);
            bw.Write(tsSec);
            bw.Write(tsUsec);
            bw.Write((uint)frameData.Length);
            bw.Write((uint)frameData.Length);
            bw.Write(frameData);
        }

        return ms.ToArray();
    }

    // ── Disposal ─────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (IsCaptureActive)
        {
            try
            {
                _activeDevice?.StopCapture();
                _activeDevice?.OnPacketArrival -= OnPacketArrival!;
                _activeDevice?.Close();
            }
            catch { /* best-effort cleanup */ }
            IsCaptureActive = false;
        }
    }
}
