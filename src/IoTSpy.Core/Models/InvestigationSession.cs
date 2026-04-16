namespace IoTSpy.Core.Models;

public class InvestigationSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string CreatedByUsername { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Random token for AirDrop / URL sharing (task 15.7).</summary>
    public string? ShareToken { get; set; }

    public ICollection<SessionCapture> SessionCaptures { get; set; } = new List<SessionCapture>();
    public ICollection<CaptureAnnotation> Annotations { get; set; } = new List<CaptureAnnotation>();
    public ICollection<SessionActivity> Activities { get; set; } = new List<SessionActivity>();
}
