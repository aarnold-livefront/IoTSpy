using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;

namespace IoTSpy.Proxy.Passive;

/// <summary>
/// Thread-safe in-memory ring buffer for passive proxy captures.
/// Capped at <see cref="MaxCapacity"/> entries; oldest are evicted when full.
/// Supports an optional device IP filter — only captures from allowed IPs are stored.
/// </summary>
public class PassiveProxyBuffer : IPassiveProxyBuffer
{
    public const int MaxCapacity = 10_000;

    private readonly LinkedList<CapturedRequest> _buffer = new();
    private readonly Lock _lock = new();
    private HashSet<string> _deviceFilter = [];

    public int Count { get { lock (_lock) return _buffer.Count; } }
    public IReadOnlySet<string> DeviceFilter { get { lock (_lock) return _deviceFilter; } }

    public void SetDeviceFilter(IEnumerable<string> ips)
    {
        lock (_lock)
            _deviceFilter = new HashSet<string>(ips, StringComparer.OrdinalIgnoreCase);
    }

    public void ClearDeviceFilter()
    {
        lock (_lock)
            _deviceFilter = [];
    }

    public void Add(CapturedRequest capture)
    {
        lock (_lock)
        {
            // Apply device filter when one is active
            if (_deviceFilter.Count > 0 && !_deviceFilter.Contains(capture.ClientIp))
                return;

            if (_buffer.Count >= MaxCapacity)
                _buffer.RemoveFirst();
            _buffer.AddLast(capture);
        }
    }

    public IReadOnlyList<CapturedRequest> Snapshot()
    {
        lock (_lock)
            return [.. _buffer];
    }

    public void Clear()
    {
        lock (_lock)
            _buffer.Clear();
    }

    public PassiveCaptureSummary GetSummary()
    {
        IReadOnlyList<CapturedRequest> snapshot;
        IReadOnlySet<string> activeFilter;
        lock (_lock)
        {
            snapshot = [.. _buffer];
            activeFilter = _deviceFilter;
        }

        var topEndpoints = snapshot
            .GroupBy(c => new { c.Method, c.Host, c.Path })
            .Select(g => new EndpointFrequency(g.Key.Method, g.Key.Host, g.Key.Path, g.Count()))
            .OrderByDescending(e => e.Count)
            .Take(20)
            .ToList();

        var statusCodes = snapshot
            .Where(c => c.StatusCode > 0)
            .GroupBy(c => c.StatusCode)
            .Select(g => new StatusCodeBucket(g.Key, g.Count()))
            .OrderByDescending(b => b.Count)
            .ToList();

        var topHosts = snapshot
            .GroupBy(c => c.Host)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => g.Key)
            .ToList();

        return new PassiveCaptureSummary(snapshot.Count, topEndpoints, statusCodes, topHosts, activeFilter);
    }
}
