namespace IoTSpy.Protocols.Mqtt;

/// <summary>
/// Represents a decoded MQTT control packet (3.1.1 or 5.0).
/// </summary>
public sealed class MqttMessage
{
    public MqttPacketType PacketType { get; init; }
    public MqttVersion Version { get; init; } = MqttVersion.Unknown;
    public int TotalLength { get; init; }

    // ── Fixed-header flags ───────────────────────────────────────────────
    public bool Duplicate { get; init; }
    public MqttQualityOfService QoS { get; init; }
    public bool Retain { get; init; }

    // ── CONNECT ──────────────────────────────────────────────────────────
    public string? ClientId { get; init; }
    public string? Username { get; init; }
    public bool HasPassword { get; init; }
    public bool CleanSession { get; init; }
    public ushort KeepAliveSeconds { get; init; }
    public string? WillTopic { get; init; }
    public byte[]? WillPayload { get; init; }
    public MqttQualityOfService WillQoS { get; init; }
    public bool WillRetain { get; init; }

    // ── CONNACK ──────────────────────────────────────────────────────────
    public bool SessionPresent { get; init; }
    public byte ConnectReturnCode { get; init; }

    // ── PUBLISH ──────────────────────────────────────────────────────────
    public string? Topic { get; init; }
    public ushort PacketId { get; init; }
    public byte[]? Payload { get; init; }

    /// <summary>Payload decoded as UTF-8 (best-effort). Null when <see cref="Payload"/> is null.</summary>
    public string? PayloadString => Payload is null ? null : System.Text.Encoding.UTF8.GetString(Payload);

    // ── SUBSCRIBE / UNSUBSCRIBE ──────────────────────────────────────────
    public IReadOnlyList<MqttSubscription>? Subscriptions { get; init; }

    // ── SUBACK ───────────────────────────────────────────────────────────
    public IReadOnlyList<byte>? GrantedQoSLevels { get; init; }

    // ── DISCONNECT (5.0) ─────────────────────────────────────────────────
    public byte ReasonCode { get; init; }

    // ── MQTT 5.0 properties ──────────────────────────────────────────────
    public IReadOnlyDictionary<byte, byte[]>? Properties { get; init; }

    // ── Raw bytes (for replay / hex dump) ────────────────────────────────
    public byte[]? RawBytes { get; init; }

    public override string ToString() =>
        Topic is not null
            ? $"MQTT {PacketType} topic={Topic} qos={QoS} len={Payload?.Length ?? 0}"
            : $"MQTT {PacketType}";
}

public sealed record MqttSubscription(string TopicFilter, MqttQualityOfService QoS);
