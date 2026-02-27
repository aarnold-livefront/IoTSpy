using IoTSpy.Core.Enums;

namespace IoTSpy.Core.Models;

public class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string IpAddress { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string Vendor { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool InterceptionEnabled { get; set; } = true;
    public DateTimeOffset FirstSeen { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.UtcNow;
    public int SecurityScore { get; set; } = -1; // -1 = unscored
    public List<CapturedRequest> Captures { get; set; } = [];
}
