namespace IoTSpy.Core.Models;

public class ReplaySession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OriginalCaptureId { get; set; }
    public CapturedRequest? OriginalCapture { get; set; }

    // Outgoing request (may differ from original)
    public string RequestMethod { get; set; } = string.Empty;
    public string RequestScheme { get; set; } = string.Empty;
    public string RequestHost { get; set; } = string.Empty;
    public int RequestPort { get; set; }
    public string RequestPath { get; set; } = string.Empty;
    public string RequestQuery { get; set; } = string.Empty;
    public string RequestHeaders { get; set; } = string.Empty;
    public string RequestBody { get; set; } = string.Empty;

    // Response
    public int? ResponseStatusCode { get; set; }
    public string ResponseHeaders { get; set; } = string.Empty;
    public string ResponseBody { get; set; } = string.Empty;
    public long? DurationMs { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
