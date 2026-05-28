using Microsoft.Extensions.Options;

namespace IoTSpy.Api.Services;

/// <summary>
/// In-memory mutable store for DataRetentionOptions. Initialized from appsettings at startup;
/// changes made via the Admin API take effect on the next retention pass but are not persisted
/// across restarts — update appsettings.json for durable configuration.
/// </summary>
public sealed class DataRetentionSettingsService
{
    private DataRetentionOptions _current;
    private readonly Lock _lock = new();

    public DataRetentionSettingsService(IOptions<DataRetentionOptions> options)
    {
        _current = options.Value;
    }

    public DataRetentionOptions Current
    {
        get { lock (_lock) return _current; }
    }

    public void Update(DataRetentionOptions opts)
    {
        lock (_lock) _current = opts;
    }
}
