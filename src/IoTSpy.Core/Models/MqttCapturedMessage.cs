namespace IoTSpy.Core.Models;

/// <summary>
/// Represents an MQTT message captured by the MQTT broker proxy.
/// </summary>
public class MqttCapturedMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ClientId { get; set; } = string.Empty;
    public string ClientIp { get; set; } = string.Empty;
    public string PacketType { get; set; } = string.Empty;
    public string? Topic { get; set; }
    public int QoS { get; set; }
    public bool Retain { get; set; }
    public string? PayloadText { get; set; }
    public long PayloadSize { get; set; }
    public string Direction { get; set; } = string.Empty; // "client→broker" or "broker→client"
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
