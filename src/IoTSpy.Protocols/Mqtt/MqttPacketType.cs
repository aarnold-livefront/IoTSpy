namespace IoTSpy.Protocols.Mqtt;

/// <summary>
/// MQTT Control Packet types (4-bit value in fixed header byte 1, bits 7-4).
/// Defined in MQTT 3.1.1 §2.2.1 and MQTT 5.0 §2.1.2.
/// </summary>
public enum MqttPacketType : byte
{
    Reserved0 = 0,
    Connect = 1,
    ConnAck = 2,
    Publish = 3,
    PubAck = 4,
    PubRec = 5,
    PubRel = 6,
    PubComp = 7,
    Subscribe = 8,
    SubAck = 9,
    Unsubscribe = 10,
    UnsubAck = 11,
    PingReq = 12,
    PingResp = 13,
    Disconnect = 14,
    Auth = 15 // MQTT 5.0 only
}
