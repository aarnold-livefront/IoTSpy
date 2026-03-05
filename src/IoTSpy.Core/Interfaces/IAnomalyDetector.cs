using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

/// <summary>
/// Records traffic observations for a host and detects statistical anomalies
/// against a rolling baseline.
/// </summary>
public interface IAnomalyDetector
{
    /// <summary>
    /// Records one traffic observation for the given host and returns any
    /// anomalies detected compared to the established baseline.
    /// </summary>
    /// <param name="host">The target host (e.g. "api.example.com").</param>
    /// <param name="responseDurationMs">Round-trip duration in milliseconds.</param>
    /// <param name="responseSizeBytes">Size of the response body in bytes.</param>
    /// <param name="statusCode">HTTP status code received.</param>
    /// <returns>Zero or more anomaly alerts triggered by this observation.</returns>
    IReadOnlyList<AnomalyAlert> Record(
        string host,
        double responseDurationMs,
        long responseSizeBytes,
        int statusCode);

    /// <summary>
    /// Returns a snapshot of the current baseline statistics for all hosts.
    /// </summary>
    IReadOnlyDictionary<string, HostBaseline> GetBaselines();

    /// <summary>
    /// Clears the baseline and observation window for the specified host.
    /// </summary>
    void Reset(string host);
}
