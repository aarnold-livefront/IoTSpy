using IoTSpy.Core.Enums;

namespace IoTSpy.Core.Models;

public class PiiStrippingLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CapturedRequestId { get; set; }
    public string Host { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string FieldPath { get; set; } = string.Empty;
    public PiiRedactionStrategy Strategy { get; set; }
    public string OriginalValueHash { get; set; } = string.Empty;
    public string RedactedPreview { get; set; } = string.Empty;
    public ManipulationPhase Phase { get; set; }
    public DateTimeOffset StrippedAt { get; set; } = DateTimeOffset.UtcNow;
}
