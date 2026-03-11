using IoTSpy.Core.Enums;

namespace IoTSpy.Core.Models;

public class NetworkDevice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    // Interface identification
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    
    // Interface type and capabilities
    public NetworkInterfaceType InterfaceType { get; set; } = NetworkInterfaceType.Ethernet;
    public bool SupportsPromiscuousMode { get; set; } = true;
    public bool IsSelectedForCapture { get; set; }
    
    // Packet statistics (live counters)
    public long PacketsCaptured { get; set; }
    public long PacketsDropped { get; set; }
    public long BytesReceived { get; set; }
    public DateTimeOffset LastPacketTime { get; set; } = DateTimeOffset.UtcNow;
    
    // Protocol statistics
    public int TcpConnectionCount { get; set; }
    public int UdpSessionCount { get; set; }
    public int ArpRequestsReceived { get; set; }
    public int DnsQueriesReceived { get; set; }
    
    // Status and metadata
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum NetworkInterfaceType
{
    Ethernet,
    Wifi,
    Loopback,
    Virtual,
    Tunnel,
    Other
}
