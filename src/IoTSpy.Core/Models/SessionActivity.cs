namespace IoTSpy.Core.Models;

/// <summary>Per-session activity log entry broadcast to all participants (task 15.5).</summary>
public class SessionActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    /// <summary>Short verb phrase, e.g. "started scan", "added rule", "joined session".</summary>
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public InvestigationSession? Session { get; set; }
}
