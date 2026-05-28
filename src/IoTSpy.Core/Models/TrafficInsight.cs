namespace IoTSpy.Core.Models;

public class TrafficInsight
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CaptureId { get; set; }
    public CapturedRequest? Capture { get; set; }

    // JSON-serialized RiskTag[] and Dictionary<RiskTag, double>
    // (same pattern as RequestHeaders/TlsMetadataJson on CapturedRequest)
    public string TagsJson { get; set; } = "[]";
    public string ConfidenceJson { get; set; } = "{}";

    public double RiskScore { get; set; }

    public string ModelVersion { get; set; } = string.Empty;

    // "rule" | "ml" | "hybrid"
    public string Source { get; set; } = "rule";

    public bool IsReviewed { get; set; }
    public bool IsDismissed { get; set; }
    public string? ReviewNote { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
