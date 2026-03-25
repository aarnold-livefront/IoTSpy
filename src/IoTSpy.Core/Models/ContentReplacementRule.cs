using IoTSpy.Core.Enums;

namespace IoTSpy.Core.Models;

public class ContentReplacementRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApiSpecDocumentId { get; set; }
    public ApiSpecDocument? ApiSpecDocument { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public ContentMatchType MatchType { get; set; }
    public string MatchPattern { get; set; } = string.Empty;
    public ContentReplacementAction Action { get; set; }
    public string? ReplacementValue { get; set; }
    public string? ReplacementFilePath { get; set; }
    public string? ReplacementContentType { get; set; }
    public string? HostPattern { get; set; }
    public string? PathPattern { get; set; }
    public int Priority { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
