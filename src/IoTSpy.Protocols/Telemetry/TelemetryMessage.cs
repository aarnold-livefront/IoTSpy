namespace IoTSpy.Protocols.Telemetry;

/// <summary>
/// A decoded telemetry message extracted from an HTTP body sent to a telemetry
/// ingestion endpoint (Datadog, AWS Firehose, Splunk HEC, or Azure Monitor).
/// </summary>
public sealed class TelemetryMessage
{
    /// <summary>Which telemetry protocol was detected.</summary>
    public TelemetryProtocol Protocol { get; init; }

    /// <summary>
    /// Source/host that sent the telemetry (e.g. Datadog hostname tag,
    /// Splunk source, Firehose stream name, Azure resource).
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Timestamp of the telemetry event (UTC). Null if not present in payload.</summary>
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>
    /// Flat key-value map of the most relevant fields extracted from the payload.
    /// For Datadog: metric name → value. For Splunk HEC: event fields.
    /// For Firehose: record fields. For Azure Monitor: log entry fields.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Fields { get; init; } =
        new Dictionary<string, object?>();

    /// <summary>Number of events / records / metrics contained in the payload.</summary>
    public int EventCount { get; init; }

    /// <summary>
    /// The raw events as parsed from the JSON body. Each element represents
    /// one metric series, log event, record, or log entry.
    /// </summary>
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Events { get; init; } =
        new List<IReadOnlyDictionary<string, object?>>();

    /// <summary>Original raw JSON body (truncated to 8 KB for display).</summary>
    public string RawJson { get; init; } = string.Empty;
}
