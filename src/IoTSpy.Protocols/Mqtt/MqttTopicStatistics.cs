namespace IoTSpy.Protocols.Mqtt;

/// <summary>Per-topic message statistics accumulated by <see cref="MqttSessionAnalyzer"/>.</summary>
public sealed class MqttTopicStatistics
{
    public string Topic { get; init; } = string.Empty;
    public long MessageCount { get; init; }
    public long TotalBytes { get; init; }
    public long RetainedCount { get; init; }
    public DateTimeOffset LastSeen { get; init; }
    public IReadOnlyDictionary<MqttQualityOfService, long> QosDistribution { get; init; } =
        new Dictionary<MqttQualityOfService, long>();
}

/// <summary>Phase of a QoS-2 packet exchange.</summary>
public enum MqttQosFlowPhase
{
    /// <summary>PUBLISH sent; waiting for PUBREC.</summary>
    Published,
    /// <summary>PUBREC received; waiting for PUBREL.</summary>
    Received,
    /// <summary>PUBREL sent; waiting for PUBCOMP.</summary>
    Released,
    /// <summary>PUBCOMP received — exchange complete.</summary>
    Completed,
}

/// <summary>Tracks one in-flight QoS-2 packet exchange.</summary>
public sealed record MqttQosFlowEntry(
    ushort PacketId,
    string? Topic,
    MqttQosFlowPhase Phase,
    DateTimeOffset LastUpdated);
