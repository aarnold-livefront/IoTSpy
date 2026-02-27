namespace IoTSpy.Core.Models;

/// <summary>
/// Strongly-typed options for outbound connection resilience (timeout / retry / circuit breaker).
/// Deserialised from the "Resilience" section of appsettings.json.
/// </summary>
public sealed class ResilienceOptions
{
    public const string SectionName = "Resilience";

    /// <summary>Maximum time allowed for a TCP connect call (seconds).</summary>
    public int ConnectTimeoutSeconds { get; set; } = 15;

    /// <summary>Maximum time allowed for a TLS handshake (seconds).</summary>
    public int TlsHandshakeTimeoutSeconds { get; set; } = 10;

    /// <summary>Number of additional attempts after the first failure.</summary>
    public int RetryCount { get; set; } = 2;

    /// <summary>Base delay between retries (seconds); grows exponentially.</summary>
    public double RetryBaseDelaySeconds { get; set; } = 0.5;

    /// <summary>Failure ratio (0-1) that trips the circuit breaker.</summary>
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    /// <summary>Sliding window width for circuit-breaker failure counting (seconds).</summary>
    public int CircuitBreakerSamplingSeconds { get; set; } = 30;

    /// <summary>How long the circuit stays open before attempting a half-open probe (seconds).</summary>
    public int CircuitBreakerBreakSeconds { get; set; } = 60;
}
