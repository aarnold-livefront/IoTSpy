namespace IoTSpy.Core.Models;

public class FuzzerResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FuzzerJobId { get; set; }
    public FuzzerJob? FuzzerJob { get; set; }

    public int MutationIndex { get; set; }
    public string MutationDescription { get; set; } = string.Empty;
    public string MutatedBody { get; set; } = string.Empty;

    public int ResponseStatusCode { get; set; }
    public string ResponseBody { get; set; } = string.Empty;
    public long DurationMs { get; set; }
    public bool IsAnomaly { get; set; }
    public string? AnomalyReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
