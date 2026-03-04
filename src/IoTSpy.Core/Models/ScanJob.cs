using IoTSpy.Core.Enums;

namespace IoTSpy.Core.Models;

public class ScanJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public Device? Device { get; set; }
    public string TargetIp { get; set; } = string.Empty;
    public ScanStatus Status { get; set; } = ScanStatus.Pending;
    public string PortRange { get; set; } = "1-1024"; // e.g. "1-1024" or "22,80,443,1883,8080"
    public int MaxConcurrency { get; set; } = 100;
    public int TimeoutMs { get; set; } = 3000;
    public bool EnableFingerprinting { get; set; } = true;
    public bool EnableCredentialTest { get; set; } = true;
    public bool EnableCveLookup { get; set; } = true;
    public bool EnableConfigAudit { get; set; } = true;
    public int TotalFindings { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public List<ScanFinding> Findings { get; set; } = [];
}
