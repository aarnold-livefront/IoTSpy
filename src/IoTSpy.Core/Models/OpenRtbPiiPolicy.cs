using IoTSpy.Core.Enums;

namespace IoTSpy.Core.Models;

public class OpenRtbPiiPolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool Enabled { get; set; } = true;
    public string FieldPath { get; set; } = string.Empty;
    public PiiRedactionStrategy Strategy { get; set; }
    public string? HostPattern { get; set; }
    public int Priority { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
