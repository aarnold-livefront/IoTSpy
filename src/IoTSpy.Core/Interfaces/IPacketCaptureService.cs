using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces
{

/// <summary>
/// Service for capturing, analyzing, and managing network packets from selected interfaces.
/// </summary>
public interface IPacketCaptureService
{
    /// <summary>
    /// List all available network interfaces with their capabilities.
    /// </summary>
    Task<IEnumerable<NetworkDevice>> ListInterfacesAsync();

    /// <summary>
    /// Get a specific network device by ID.
    /// </summary>
    Task<NetworkDevice?> GetByIdAsync(Guid id);

    /// <summary>
    /// Select an interface for packet capture (activates promiscuous mode if supported).
    /// </summary>
    Task<bool> SelectInterfaceForCaptureAsync(Guid deviceId, CancellationToken ct = default);

    /// <summary>
    /// Deselect the current capture interface.
    /// </summary>
    Task<bool> DeselectCurrentInterfaceAsync();

    /// <summary>
    /// Start capturing packets from the selected interface.
    /// </summary>
    Task<bool> StartCaptureAsync(Guid deviceId, CancellationToken ct = default);

    /// <summary>
    /// Stop packet capture.
    /// </summary>
    Task<bool> StopCaptureAsync();

    /// <summary>
    /// Check if capture is currently active.
    /// </summary>
    bool IsCaptureActive { get; }

    /// <summary>
    /// Get live statistics for the selected interface.
    /// </summary>
    Task<NetworkDeviceStatistics?> GetLiveStatsAsync(Guid deviceId);

    /// <summary>
    /// Get captured packets with filtering and pagination.
    /// </summary>
    Task<PagedResult<CapturedPacket>> GetCapturedPacketsAsync(
        PacketFilter filter, 
        int page = 1, 
        int pageSize = 50, 
        CancellationToken ct = default);

    /// <summary>
    /// Filter packets based on criteria and return results.
    /// </summary>
    Task<IEnumerable<CapturedPacket>> FilterPacketsAsync(PacketFilterDto filter, CancellationToken ct = default);

    /// <summary>
    /// Get a specific packet by ID.
    /// </summary>
    Task<CapturedPacket?> GetPacketByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Create freeze frame for a specific packet with detailed analysis.
    /// </summary>
    Task<FreezeFrameResult?> FreezeFrameAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Get an existing freeze frame for inspection.
    /// </summary>
    Task<FreezeFrameResult?> GetFreezeFrameAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Delete a specific packet.
    /// </summary>
    Task<bool> DeletePacketAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Clear all captured packets.
    /// </summary>
    Task<bool> ClearCapturesAsync();

    /// <summary>
    /// Export captured packets to PCAP file format.
    /// </summary>
    Task<byte[]?> ExportToPcapAsync(CancellationToken ct = default);

    /// <summary>
    /// Export filtered captured packets to PCAP file format.
    /// </summary>
    Task<byte[]?> ExportToPcapFilteredAsync(PacketFilterDto filter, CancellationToken ct = default);

    /// <summary>
    /// Import packets from a PCAP or pcapng file stream.
    /// Returns import statistics. Streams progress via the packet publisher.
    /// </summary>
    Task<PcapImportResult> ImportFromPcapAsync(Stream pcapStream, string fileName, CancellationToken ct = default);
}

/// <summary>
/// Result returned after a PCAP import operation.
/// </summary>
public class PcapImportResult
{
    public string JobId { get; set; } = string.Empty;
    public int PacketsImported { get; set; }
    public int PacketsSkipped { get; set; }
    public int TcpSessionsReconstructed { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Service for analyzing captured packets with filtering and freeze frame support.
/// </summary>
public interface IPacketCaptureAnalyzer
{

    /// <summary>
    /// Freeze the current filtered view for inspection.
    /// </summary>
    void FreezeFrame();

    /// <summary>
    /// Unfreeze and resume live updates.
    /// </summary>
    void UnfreezeFrame();

    /// <summary>
    /// Check if currently frozen.
    /// </summary>
    bool IsFrozen { get; }

    /// <summary>
    /// Get the current filtered packet count.
    /// </summary>
    int FilteredPacketCount { get; }

    /// <summary>
    /// Get packets from the frozen frame or live set (if not frozen).
    /// </summary>
    IEnumerable<CapturedPacket> GetFilteredPackets();

    /// <summary>
    /// Analyze protocol distribution in captured packets.
    /// </summary>
    Task<ProtocolDistribution?> AnalyzeProtocolsAsync(CancellationToken ct = default);

    /// <summary>
    /// Find communication patterns between hosts.
    /// </summary>
    Task<List<CommunicationPattern>> FindCommunicationPatternsAsync(int topN = 10, CancellationToken ct = default);

    /// <summary>
    /// Detect suspicious activity based on heuristics.
    /// </summary>
    Task<List<SuspiciousActivity>> DetectSuspiciousActivityAsync(CancellationToken ct = default);
}

/// <summary>
/// Live statistics for a network interface during capture.
/// </summary>
public class NetworkDeviceStatistics
{
    public Guid DeviceId { get; set; }
    public long PacketsPerSecond { get; set; }
    public long BytesPerSecond { get; set; }
    public long TotalPacketsCaptured { get; set; }
    public long TotalPacketsDropped { get; set; }
    public long TotalBytesReceived { get; set; }
    public int TcpConnections { get; set; }
    public int UdpSessions { get; set; }
    public double DropRatePercent { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Protocol distribution statistics.
/// </summary>
public class ProtocolDistribution
{
    public int TotalPackets { get; set; }
    public List<ProtocolStats> ByProtocol { get; set; } = new();
    public List<ProtocolStats> ByLayer3 { get; set; } = new();
    public List<ProtocolStats> ByLayer4 { get; set; } = new();
}

public class ProtocolStats
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
}

/// <summary>
/// Communication pattern between two hosts.
/// </summary>
public class CommunicationPattern
{
    public string SourceIp { get; set; } = string.Empty;
    public string DestinationIp { get; set; } = string.Empty;
    public int PacketCount { get; set; }
    public long TotalBytes { get; set; }
    public List<string> ProtocolsUsed { get; set; } = new();
    public DateTimeOffset? FirstSeen { get; set; }
    public DateTimeOffset? LastSeen { get; set; }
}

/// <summary>
/// Suspicious activity detection result.
/// </summary>
public class SuspiciousActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Category { get; set; } = string.Empty;  // PortScan, ArpSpoofing, DnsAnomaly, etc.
    public string Severity { get; set; } = "Medium";     // Low, Medium, High, Critical
    public string Description { get; set; } = string.Empty;
    public string SourceIp { get; set; } = string.Empty;
    public string? DestinationIp { get; set; }
    public int PacketCount { get; set; }
    public DateTimeOffset FirstDetected { get; set; }
    public List<string> Evidence { get; set; } = new();
}

/// <summary>
    /// Paginated result for packet queries.
    /// </summary>
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);
    }

    /// <summary>
    /// Packet filter criteria for API requests.
    /// </summary>
    public class PacketFilterDto
    {
        public Guid? DeviceId { get; set; }
        public string? Protocol { get; set; }
        public string? SourceIp { get; set; }
        public string? DestinationIp { get; set; }
        public int? SourcePort { get; set; }
        public int? DestinationPort { get; set; }
        public string? MacAddress { get; set; }
        public bool ShowOnlyErrors { get; set; }
        public bool ShowOnlyRetransmissions { get; set; }
        public DateTime? FromTime { get; set; }
        public DateTime? ToTime { get; set; }
        public string? PayloadSearch { get; set; }
        public int Limit { get; set; } = 1000;
    }

    /// <summary>
    /// Freeze frame result with detailed packet analysis.
    /// </summary>
    public class FreezeFrameResult
    {
        public Guid PacketId { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public string FullPayloadHex { get; set; } = "";
        public string HexDump { get; set; } = "";
        public string ProtocolDetails { get; set; } = "";
        public string Layer2Info { get; set; } = "";
        public string Layer3Info { get; set; } = "";
        public string Layer4Info { get; set; } = "";
    }

}

