using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using System.Text;
using System.Threading.Channels;

namespace IoTSpy.Scanner;

/// <summary>
/// Live packet capture via SharpPcap/libpcap.
///
/// Performance architecture (Options A + B + C):
///   A — <see cref="OnPacketArrival"/> writes to a bounded <see cref="Channel{T}"/>
///       (non-blocking, no lock), fully decoupling the SharpPcap callback thread from
///       storage and publishing.
///   B — The Channel consumer writes parsed packets into a <see cref="IPacketBuffer"/>
///       (lock-free ring array); query endpoints take lock-free snapshots.
///   C — The consumer accumulates up to <see cref="ConsumerBatchSize"/> packets or
///       waits <see cref="ConsumerBatchMs"/> ms, then emits a single SignalR batch
///       message instead of one message per packet.
/// </summary>
public sealed class PacketCaptureService : IPacketCaptureService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPacketCapturePublisher _publisher;
    private readonly IPacketBuffer _buffer;
    private readonly ILogger<PacketCaptureService> _logger;

    private ILiveDevice? _activeDevice;
    private Guid _activeDeviceId;
    private long _captureIndex;
    private bool _devicesEnumerated;

    // Channel lifecycle — recreated on each StartCaptureAsync call.
    private Channel<CapturedPacket> _channel = CreateChannel();
    private Task _consumerTask = Task.CompletedTask;
    private CancellationTokenSource _consumerCts = new();

    private const int ChannelCapacity = 50_000;
    private const int ConsumerBatchSize = 50;
    private const int ConsumerBatchMs = 50;

    public bool IsCaptureActive { get; private set; }

    public PacketCaptureService(
        IServiceScopeFactory scopeFactory,
        IPacketCapturePublisher publisher,
        IPacketBuffer buffer,
        ILogger<PacketCaptureService> logger)
    {
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _buffer = buffer;
        _logger = logger;
    }

    private static Channel<CapturedPacket> CreateChannel() =>
        Channel.CreateBounded<CapturedPacket>(new BoundedChannelOptions(ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,  // SharpPcap uses a single callback thread per device
            SingleReader = true
        });

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
                    Description = dev is LibPcapLiveDevice ld ? ld.Description ?? dev.Name : dev.Name,
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

            _buffer.Clear();
            _activeDeviceId = deviceId;
            Interlocked.Exchange(ref _captureIndex, 0);

            // Fresh channel + consumer for this capture session.
            _consumerCts.Cancel();
            _consumerCts.Dispose();
            _consumerCts = new CancellationTokenSource();
            _channel = CreateChannel();
            _consumerTask = Task.Run(() => RunConsumerAsync(_consumerCts.Token));

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

            // Signal consumer to finish and drain the channel.
            _channel.Writer.TryComplete();
            _consumerCts.Cancel();
            try { await _consumerTask.WaitAsync(TimeSpan.FromSeconds(3)); }
            catch (TimeoutException) { _logger.LogWarning("Packet consumer did not drain within 3 s"); }

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

    // ── Packet arrival (SharpPcap capture thread — must not block) ───────────

    private void OnPacketArrival(object sender, PacketCapture e)
    {
        try
        {
            var packet = ParseRawPacket(e.GetPacket());
            // TryWrite is non-blocking and thread-safe; DropOldest evicts if full.
            _channel.Writer.TryWrite(packet);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error processing captured packet");
        }
    }

    // ── Channel consumer (Option A + C) ─────────────────────────────────────

    private async Task RunConsumerAsync(CancellationToken ct)
    {
        var batch = new List<CapturedPacket>(ConsumerBatchSize);

        while (!ct.IsCancellationRequested)
        {
            // Wait up to ConsumerBatchMs for new data, then flush whatever we have.
            using var flushCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            flushCts.CancelAfter(ConsumerBatchMs);

            try
            {
                if (!await _channel.Reader.WaitToReadAsync(flushCts.Token))
                    break; // channel writer completed
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Batch window elapsed — fall through to publish what's available.
            }

            while (batch.Count < ConsumerBatchSize && _channel.Reader.TryRead(out var pkt))
                batch.Add(pkt);

            if (batch.Count > 0)
            {
                foreach (var pkt in batch)
                    _buffer.Add(pkt);

                try { await _publisher.PublishPacketBatchAsync(batch, ct); }
                catch (Exception ex) { _logger.LogDebug(ex, "Failed to publish packet batch"); }

                batch.Clear();
            }
        }

        // Drain any remaining packets after stop signal.
        while (_channel.Reader.TryRead(out var pkt))
            batch.Add(pkt);

        if (batch.Count > 0)
        {
            foreach (var pkt in batch)
                _buffer.Add(pkt);
        }
    }

    // ── Queries (no lock — all read from the lock-free ring buffer) ──────────

    public Task<NetworkDeviceStatistics?> GetLiveStatsAsync(Guid deviceId)
    {
        if (_activeDeviceId != deviceId)
            return Task.FromResult<NetworkDeviceStatistics?>(null);

        return Task.FromResult<NetworkDeviceStatistics?>(new NetworkDeviceStatistics
        {
            DeviceId = deviceId,
            TotalPacketsCaptured = Interlocked.Read(ref _captureIndex),
            PacketsPerSecond = 0,
            Timestamp = DateTimeOffset.UtcNow
        });
    }

    public Task<PagedResult<CapturedPacket>> GetCapturedPacketsAsync(
        PacketFilter filter,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        // Snapshot outside any lock; filtering happens on the snapshot copy.
        var filtered = ApplyFilter(_buffer.Snapshot(), filter);
        var total = filtered.Count;
        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

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
                ? new DateTimeOffset(filter.FromTime.Value, TimeSpan.Zero) : null,
            ToTime = filter.ToTime.HasValue
                ? new DateTimeOffset(filter.ToTime.Value, TimeSpan.Zero) : null,
            PayloadSearch = filter.PayloadSearch
        };

        var result = ApplyFilter(_buffer.Snapshot(), pf);
        return Task.FromResult<IEnumerable<CapturedPacket>>(result.Take(filter.Limit).ToList());
    }

    public Task<CapturedPacket?> GetPacketByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_buffer.GetById(id));

    public Task<FreezeFrameResult?> FreezeFrameAsync(Guid id, CancellationToken ct = default)
    {
        var packet = _buffer.GetById(id);
        if (packet == null) return Task.FromResult<FreezeFrameResult?>(null);
        var result = BuildFreezeFrame(packet);
        _buffer.SetFreezeFrame(id, result);
        return Task.FromResult<FreezeFrameResult?>(result);
    }

    public Task<FreezeFrameResult?> GetFreezeFrameAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_buffer.GetFreezeFrame(id));

    public Task<bool> DeletePacketAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_buffer.TryDelete(id));

    public Task<bool> ClearCapturesAsync()
    {
        _buffer.Clear();
        return Task.FromResult(true);
    }

    // ── PCAP export ──────────────────────────────────────────────────────────

    public Task<byte[]?> ExportToPcapAsync(CancellationToken ct = default)
    {
        var snapshot = _buffer.Snapshot();
        if (snapshot.Length == 0)
            return Task.FromResult<byte[]?>(null);

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        WritePcapGlobalHeader(bw);

        foreach (var p in snapshot)
        {
            var frameData = p.RawData;
            if (frameData is null || frameData.Length == 0) continue;

            var tsSec = (uint)p.Timestamp.ToUnixTimeSeconds();
            var tsUsec = (uint)(p.Timestamp.Millisecond * 1000);
            bw.Write(tsSec); bw.Write(tsUsec);
            bw.Write((uint)frameData.Length); bw.Write((uint)frameData.Length);
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
        WritePcapGlobalHeader(bw);

        foreach (var p in filtered)
        {
            var frameData = p.RawData; // still in memory — RawData is [NotMapped]
            if (frameData is null || frameData.Length == 0) continue;

            var tsSec = (uint)p.Timestamp.ToUnixTimeSeconds();
            var tsUsec = (uint)(p.Timestamp.Millisecond * 1000);
            bw.Write(tsSec); bw.Write(tsUsec);
            bw.Write((uint)frameData.Length); bw.Write((uint)frameData.Length);
            bw.Write(frameData);
        }

        return ms.ToArray();
    }

    private static void WritePcapGlobalHeader(BinaryWriter bw)
    {
        bw.Write(0xa1b2c3d4u);  // magic
        bw.Write((ushort)2); bw.Write((ushort)4);  // version
        bw.Write(0); bw.Write(0u);  // thiszone, sigfigs
        bw.Write(65535u);  // snaplen
        bw.Write(1u);      // LINKTYPE_ETHERNET
    }

    // ── PCAP import ─────────────────────────────────────────────────────────

    public async Task<PcapImportResult> ImportFromPcapAsync(
        Stream pcapStream,
        string fileName,
        CancellationToken ct = default)
    {
        var jobId = Guid.NewGuid().ToString("N")[..8];
        var result = new PcapImportResult { JobId = jobId };

        var tempPath = Path.Combine(Path.GetTempPath(), $"iotspy_import_{jobId}.pcap");
        try
        {
            await using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                await pcapStream.CopyToAsync(fs, ct);

            List<RawCapture> rawCaptures = ReadAllRawCaptures(tempPath, ct);
            int total = rawCaptures.Count;

            await _publisher.PublishImportProgressAsync(jobId, 0, total, ct);

            var imported = new List<CapturedPacket>(Math.Min(total, _buffer.Capacity));
            long index = Interlocked.Read(ref _captureIndex);

            for (int i = 0; i < rawCaptures.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var packet = ParseRawPacket(rawCaptures[i]);
                    packet.Timestamp = new DateTimeOffset(rawCaptures[i].Timeval.Date);
                    packet.CaptureIndex = ++index;
                    packet.Source = "Import";
                    imported.Add(packet);
                    result.PacketsImported++;
                }
                catch { result.PacketsSkipped++; }

                int processed = i + 1;
                if (processed % 100 == 0 || processed == total)
                    await _publisher.PublishImportProgressAsync(jobId, processed, total, ct);
            }

            Interlocked.Exchange(ref _captureIndex, index);

            _buffer.Clear();
            foreach (var pkt in imported.TakeLast(_buffer.Capacity))
                _buffer.Add(pkt);

            result.TcpSessionsReconstructed = TcpSessionReconstructor.ReconstructSessions(imported);

            // Publish imported packets in batches (Option C) to avoid flooding SignalR.
            const int importBatch = 50;
            for (int i = 0; i < imported.Count; i += importBatch)
            {
                ct.ThrowIfCancellationRequested();
                var chunk = imported.Skip(i).Take(importBatch).ToList();
                await _publisher.PublishPacketBatchAsync(chunk, ct);
            }

            result.Success = true;
            _logger.LogInformation(
                "PCAP import {JobId}: {Imported} packets imported, {Sessions} HTTP sessions reconstructed",
                jobId, result.PacketsImported, result.TcpSessionsReconstructed);
        }
        catch (OperationCanceledException) { result.Error = "Import cancelled"; }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            _logger.LogError(ex, "PCAP import {JobId} failed", jobId);
        }
        finally
        {
            try { File.Delete(tempPath); } catch { /* best-effort */ }
        }

        return result;
    }

    private static List<RawCapture> ReadAllRawCaptures(string filePath, CancellationToken ct)
    {
        var captures = new List<RawCapture>();
        using var device = new CaptureFileReaderDevice(filePath);
        device.Open();
        while (!ct.IsCancellationRequested &&
               device.GetNextPacket(out var cap) == GetPacketStatus.PacketRead)
        {
            captures.Add(cap.GetPacket());
        }
        return captures;
    }

    // ── Packet parsing ───────────────────────────────────────────────────────

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
            RawData = rawCapture.Data,
            Protocol = "Unknown",
            Layer2Protocol = "Ethernet",
            Layer3Protocol = "Unknown",
            Layer4Protocol = "Unknown",
        };

        try
        {
            var parsed = Packet.ParsePacket(rawCapture.LinkLayerType, rawCapture.Data);
            if (parsed == null) return packet;

            var eth = parsed.Extract<EthernetPacket>();
            if (eth != null)
            {
                packet.Layer2Protocol = "Ethernet";
                packet.SourceMac = eth.SourceHardwareAddress?.ToString() ?? string.Empty;
                packet.DestinationMac = eth.DestinationHardwareAddress?.ToString() ?? string.Empty;
            }

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

            var ip4 = parsed.Extract<IPv4Packet>();
            if (ip4 != null)
            {
                packet.Layer3Protocol = "IPv4";
                packet.SourceIp = ip4.SourceAddress.ToString();
                packet.DestinationIp = ip4.DestinationAddress.ToString();
                packet.IsFragment = ip4.FragmentOffset != 0;
            }

            var ip6 = parsed.Extract<IPv6Packet>();
            if (ip6 != null)
            {
                packet.Layer3Protocol = "IPv6";
                packet.SourceIp = ip6.SourceAddress.ToString();
                packet.DestinationIp = ip6.DestinationAddress.ToString();
            }

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

    private static string? ResolveAppProtocol(int srcPort, int dstPort) =>
        MatchPort(srcPort) ?? MatchPort(dstPort);

    private static string? MatchPort(int port) => port switch
    {
        80 => "HTTP", 443 => "HTTPS", 53 => "DNS",
        1883 => "MQTT", 8883 => "MQTT/TLS", 5353 => "mDNS",
        5683 => "CoAP", 67 or 68 => "DHCP",
        22 => "SSH", 23 => "Telnet",
        _ => null
    };

    private static string GetPayloadPreview(byte[] data)
    {
        var preview = data.AsSpan(0, Math.Min(data.Length, 128));
        var sb = new StringBuilder(preview.Length);
        foreach (var b in preview)
            sb.Append(b >= 0x20 && b < 0x7F ? (char)b : '.');
        return sb.ToString();
    }

    private static List<CapturedPacket> ApplyFilter(IEnumerable<CapturedPacket> source, PacketFilter f)
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

    private static FreezeFrameResult BuildFreezeFrame(CapturedPacket packet)
    {
        var rawBytes = packet.RawData ?? Encoding.UTF8.GetBytes(packet.PayloadPreview);
        return new FreezeFrameResult
        {
            PacketId = packet.Id,
            Timestamp = packet.Timestamp,
            FullPayloadHex = BitConverter.ToString(rawBytes).Replace("-", " "),
            HexDump = FormatHexDump(rawBytes),
            ProtocolDetails = $"{packet.Protocol} ({packet.Layer4Protocol})",
            Layer2Info = $"{packet.Layer2Protocol} src={packet.SourceMac} dst={packet.DestinationMac}",
            Layer3Info = $"{packet.Layer3Protocol} src={packet.SourceIp} dst={packet.DestinationIp}",
            Layer4Info = packet.TcpFlags != null
                ? $"TCP src={packet.SourcePort} dst={packet.DestinationPort} flags={packet.TcpFlags}"
                : $"{packet.Layer4Protocol} src={packet.SourcePort} dst={packet.DestinationPort}"
        };
    }

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
        _consumerCts.Cancel();
        _consumerCts.Dispose();
    }
}
