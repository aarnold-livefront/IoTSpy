namespace IoTSpy.Analytics.Features;

public sealed record FeatureVector
{
    // Numeric (log-transformed where skewed)
    public float ResponseBodySizeLog { get; init; }
    public float RequestBodySizeLog { get; init; }
    public float DurationMsLog { get; init; }
    public float Port { get; init; }
    public float StatusCode { get; init; }
    public float TlsCipherStrength { get; init; }  // 0=known-weak, 1=unknown, 2=modern
    public float HourOfDay { get; init; }
    public float DayOfWeek { get; init; }
    public float DnsNameLength { get; init; }
    public float DnsNameEntropy { get; init; }

    // Boolean (0.0 / 1.0)
    public float IsTls { get; init; }
    public float IsStandardPort { get; init; }
    public float IsModified { get; init; }
    public float HostIsIp { get; init; }
    public float HasUserAgent { get; init; }
    public float HasAuthorization { get; init; }
    public float ContentTypeIsJson { get; init; }
    public float ContentTypeIsBinary { get; init; }

    // Total feature count (without text-derived LSA components, which are added dynamically)
    public static int BaseFeatureCount => 18;

    public float[] ToArray() =>
    [
        ResponseBodySizeLog, RequestBodySizeLog, DurationMsLog,
        Port, StatusCode, TlsCipherStrength,
        HourOfDay, DayOfWeek, DnsNameLength, DnsNameEntropy,
        IsTls, IsStandardPort, IsModified, HostIsIp,
        HasUserAgent, HasAuthorization, ContentTypeIsJson, ContentTypeIsBinary
    ];
}
