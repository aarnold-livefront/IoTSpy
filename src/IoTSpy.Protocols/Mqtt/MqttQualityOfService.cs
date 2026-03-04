namespace IoTSpy.Protocols.Mqtt;

public enum MqttQualityOfService : byte
{
    AtMostOnce = 0,
    AtLeastOnce = 1,
    ExactlyOnce = 2
}
