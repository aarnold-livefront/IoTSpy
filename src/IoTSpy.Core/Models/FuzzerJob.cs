using IoTSpy.Core.Enums;

namespace IoTSpy.Core.Models;

public class FuzzerJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BaseCaptureId { get; set; }
    public CapturedRequest? BaseCapture { get; set; }

    public FuzzerStrategy Strategy { get; set; }
    public int MutationCount { get; set; } = 50;
    public int ConcurrentRequests { get; set; } = 5;
    public FuzzerJobStatus Status { get; set; } = FuzzerJobStatus.Pending;
    public int CompletedMutations { get; set; }
    public int Anomalies { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public List<FuzzerResult> Results { get; set; } = [];
}
