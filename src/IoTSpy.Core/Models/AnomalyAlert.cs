namespace IoTSpy.Core.Models;

/// <summary>
/// Represents a detected traffic anomaly for a monitored host.
/// </summary>
public class AnomalyAlert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Type of anomaly: ResponseTime, ResponseSize, StatusCode, RequestRate.
    /// </summary>
    public string AlertType { get; set; } = string.Empty;

    public double ExpectedValue { get; set; }
    public double ActualValue { get; set; }

    /// <summary>
    /// How many standard deviations the actual value is from the expected value.
    /// </summary>
    public double DeviationFactor { get; set; }

    public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.UtcNow;
}
