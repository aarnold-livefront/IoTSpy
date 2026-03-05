namespace IoTSpy.Protocols.Telemetry;

/// <summary>
/// Identifies which telemetry ingestion protocol was detected.
/// </summary>
public enum TelemetryProtocol
{
    Unknown = 0,
    Datadog = 1,
    AwsFirehose = 2,
    SplunkHec = 3,
    AzureMonitor = 4
}
