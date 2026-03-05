namespace IoTSpy.Core.Enums;

public enum PiiRedactionStrategy
{
    Redact,
    TruncateIp,
    HashSha256,
    GeneralizeGeo,
    GeneralizeUserAgent,
    Remove
}
