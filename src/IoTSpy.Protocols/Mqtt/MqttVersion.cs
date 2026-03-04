namespace IoTSpy.Protocols.Mqtt;

public enum MqttVersion : byte
{
    Unknown = 0,
    V311 = 4,  // MQTT 3.1.1, protocol level 4
    V500 = 5   // MQTT 5.0, protocol level 5
}
