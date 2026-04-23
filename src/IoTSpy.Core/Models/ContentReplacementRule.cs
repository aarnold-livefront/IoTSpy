using IoTSpy.Core.Enums;

namespace IoTSpy.Core.Models;

public class ContentReplacementRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ApiSpecDocumentId { get; set; }
    public ApiSpecDocument? ApiSpecDocument { get; set; }

    /// <summary>Hostname for standalone rules (no spec). Required when ApiSpecDocumentId is null.</summary>
    public string? Host { get; set; }
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

    /// <summary>
    /// For <see cref="ContentReplacementAction.MockSseStream"/>: delay (milliseconds)
    /// between emitted events. 0 = flush events as fast as possible.
    /// </summary>
    public int? SseInterEventDelayMs { get; set; }

    /// <summary>
    /// For <see cref="ContentReplacementAction.MockSseStream"/>: when true, replay the
    /// event file in a loop until the client disconnects. When false (default), emit
    /// the file once and close the connection.
    /// </summary>
    public bool? SseLoop { get; set; }
}
