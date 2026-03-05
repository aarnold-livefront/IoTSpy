using IoTSpy.Core.Enums;

namespace IoTSpy.Core.Models;

public class OpenRtbEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CapturedRequestId { get; set; }
    public string Version { get; set; } = string.Empty;
    public OpenRtbMessageType MessageType { get; set; }
    public int ImpressionCount { get; set; }
    public int BidCount { get; set; }
    public bool HasDeviceInfo { get; set; }
    public bool HasUserData { get; set; }
    public bool HasGeoData { get; set; }
    public string Exchange { get; set; } = string.Empty;
    public string? SeatBids { get; set; }
    public string RawJson { get; set; } = string.Empty;
    public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.UtcNow;
}
