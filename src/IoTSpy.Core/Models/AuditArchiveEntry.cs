namespace IoTSpy.Core.Models;

/// <summary>
/// Audit entries moved from AuditEntries once they exceed the configured retention window.
/// Append-only by convention; the admin UI does not expose a direct delete action.
/// See ADR 0002.
/// </summary>
public class AuditArchiveEntry
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public DateTimeOffset ArchivedAt { get; set; }
}
