namespace IoTSpy.Core.Models;

/// <summary>Join table linking a <see cref="CapturedRequest"/> to an <see cref="InvestigationSession"/>.</summary>
public class SessionCapture
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid CaptureId { get; set; }
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid AddedByUserId { get; set; }

    public InvestigationSession? Session { get; set; }
    public CapturedRequest? Capture { get; set; }
}
