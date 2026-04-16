namespace IoTSpy.Core.Models;

public class CaptureAnnotation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid CaptureId { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    /// <summary>Comma-separated tags (e.g. "suspicious,auth,pii").</summary>
    public string? Tags { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    public InvestigationSession? Session { get; set; }
}
