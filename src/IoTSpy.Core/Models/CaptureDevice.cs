using System.ComponentModel.DataAnnotations.Schema;
using IoTSpy.Core.Interfaces;

namespace IoTSpy.Core.Models
{
    /// <summary>
    /// A network device/interface available for packet capture.
    /// </summary>
    public class CaptureDevice
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public bool CanCapture { get; set; }
        public bool SupportsPromiscuousMode { get; set; }
        
        // Navigation property for EF Core
        public List<CapturedPacket> Packets { get; set; } = new();
    }

    /// <summary>
    /// Packet filter criteria for analysis queries.
    /// </summary>
    public class PacketFilter
    {
        public string? Protocol { get; set; }
        public string? SourceIp { get; set; }
        public string? DestinationIp { get; set; }
        public int? SourcePort { get; set; }
        public int? DestinationPort { get; set; }
        public string? MacAddress { get; set; }
        public bool ShowOnlyErrors { get; set; }
        public bool ShowOnlyRetransmissions { get; set; }
        public DateTimeOffset? FromTime { get; set; }
        public DateTimeOffset? ToTime { get; set; }
        public string? PayloadSearch { get; set; }
    }

    /// <summary>
    /// A captured network packet.
    /// </summary>
    public class CapturedPacket
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public long CaptureIndex { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public double TimeDeltaFromPrevious { get; set; }
        public Guid DeviceId { get; set; }
        public string Protocol { get; set; } = "Unknown";
        public string Layer2Protocol { get; set; } = "Ethernet";
        public string Layer3Protocol { get; set; } = "IPv4";
        public string Layer4Protocol { get; set; } = "TCP/UDP";
        public string SourceIp { get; set; } = string.Empty;
        public string DestinationIp { get; set; } = string.Empty;
        public int? SourcePort { get; set; }
        public int? DestinationPort { get; set; }
        public string SourceMac { get; set; } = string.Empty;
        public string DestinationMac { get; set; } = string.Empty;
        public int Length { get; set; }
        public int FrameNumber { get; set; }
        public bool IsError { get; set; }
        public bool IsRetransmission { get; set; }
        public bool IsFragment { get; set; }
        public string? TcpFlags { get; set; }
        public string? UdpLength { get; set; }
        public string? ArpOperation { get; set; }
        public string? ArpSenderMac { get; set; }
        public string? ArpTargetIp { get; set; }
        public string? DnsQueryName { get; set; }
        public string? DnsResponseType { get; set; }
        public string? HttpMethodName { get; set; }
        public string? HttpRequestUri { get; set; }
        public int? HttpResponseCode { get; set; }
        public string PayloadPreview { get; set; } = string.Empty;
        public bool IsSuspicious { get; set; }
        public string? SuspicionReason { get; set; }

        /// <summary>
        /// Raw frame bytes — only populated in memory during live capture or PCAP import.
        /// Not persisted to DB.
        /// </summary>
        [NotMapped]
        public byte[]? RawData { get; set; }

        /// <summary>
        /// Origin of the packet: "Live" for real-time capture, "Import" for PCAP file imports.
        /// Not persisted to DB.
        /// </summary>
        [NotMapped]
        public string Source { get; set; } = "Live";

        // Navigation property for EF Core
        public CaptureDevice Device { get; set; } = null!;
    }
}
