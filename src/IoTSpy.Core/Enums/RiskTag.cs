namespace IoTSpy.Core.Enums;

public enum RiskTag
{
    ExfiltrationRisk,
    PiiDetected,
    DataBroker,
    SuspiciousTls,
    UnusualPort,
    MqttCredentialExposure,
    DnsTunneling,
    HighEntropyPayload
}
