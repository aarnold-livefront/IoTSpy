namespace IoTSpy.Storage.Redis;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>
    /// StackExchange.Redis connection string.
    /// When null or empty, the in-memory <c>PassiveProxyBuffer</c> is used instead.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>Prefix applied to all Redis keys owned by IoTSpy.</summary>
    public string KeyPrefix { get; set; } = "iotspy";

    /// <summary>
    /// Maximum entries kept in the passive capture ring buffer.
    /// Oldest entries are evicted when this limit is reached.
    /// </summary>
    public int PassiveBufferCapacity { get; set; } = 10_000;

    /// <summary>
    /// Wire up the Redis SignalR backplane so broadcasts propagate across
    /// multiple API instances. Only meaningful when running more than one pod.
    /// </summary>
    public bool EnableSignalRBackplane { get; set; } = true;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);
}
