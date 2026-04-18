namespace IoTSpy.Core.Models;

public class PassiveCaptureSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public int EntryCount { get; set; }
    public string? DeviceFilter { get; set; }
}
