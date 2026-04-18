using System.Text.Json;
using System.Text.Json.Serialization;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using StackExchange.Redis;

namespace IoTSpy.Storage.Redis;

/// <summary>
/// Redis-backed implementation of <see cref="IPassiveProxyBuffer"/>.
/// Replaces the in-process <c>PassiveProxyBuffer</c> when a Redis connection
/// is configured, enabling the ring buffer to survive API restarts and to be
/// shared across multiple API instances.
///
/// Key layout:
///   {prefix}:passive:buffer  — Redis List (JSON entries, newest at tail)
///   {prefix}:passive:filter  — Redis Set  (allowed client IP strings)
/// </summary>
public sealed class RedisPassiveProxyBuffer : IPassiveProxyBuffer
{
    private readonly IDatabase _db;
    private readonly string _bufferKey;
    private readonly string _filterKey;
    private readonly int _maxCapacity;
    private readonly JsonSerializerOptions _json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    // Local cache of the device-IP filter — avoids a Redis round-trip on
    // every Add() call while keeping Redis as the source of truth.
    private HashSet<string> _filterCache;
    private readonly Lock _filterLock = new();

    public RedisPassiveProxyBuffer(IConnectionMultiplexer redis, RedisOptions options)
    {
        _db = redis.GetDatabase();
        _bufferKey = $"{options.KeyPrefix}:passive:buffer";
        _filterKey = $"{options.KeyPrefix}:passive:filter";
        _maxCapacity = options.PassiveBufferCapacity;

        // Hydrate the local filter cache from Redis on startup.
        var members = _db.SetMembers(_filterKey);
        _filterCache = new HashSet<string>(
            members.Select(m => m.ToString()),
            StringComparer.OrdinalIgnoreCase);
    }

    public int Count => (int)_db.ListLength(_bufferKey);

    public IReadOnlySet<string> DeviceFilter
    {
        get { lock (_filterLock) return _filterCache; }
    }

    public void Add(CapturedRequest capture)
    {
        HashSet<string> filter;
        lock (_filterLock) filter = _filterCache;

        if (filter.Count > 0 && !filter.Contains(capture.ClientIp))
            return;

        var json = JsonSerializer.Serialize(capture, _json);
        _db.ListRightPush(_bufferKey, json);
        // LTRIM with negative indices keeps the newest N entries.
        _db.ListTrim(_bufferKey, -_maxCapacity, -1);
    }

    public IReadOnlyList<CapturedRequest> Snapshot()
    {
        var entries = _db.ListRange(_bufferKey, 0, -1);
        var results = new List<CapturedRequest>(entries.Length);
        foreach (var entry in entries)
        {
            try
            {
                var capture = JsonSerializer.Deserialize<CapturedRequest>(entry.ToString(), _json);
                if (capture is not null)
                    results.Add(capture);
            }
            catch (JsonException) { /* skip corrupt entries */ }
        }
        return results;
    }

    public void Clear() => _db.KeyDelete(_bufferKey);

    public void SetDeviceFilter(IEnumerable<string> ips)
    {
        var set = new HashSet<string>(ips, StringComparer.OrdinalIgnoreCase);
        lock (_filterLock) _filterCache = set;

        _db.KeyDelete(_filterKey);
        if (set.Count > 0)
            _db.SetAdd(_filterKey, set.Select(ip => (RedisValue)ip).ToArray());
    }

    public void ClearDeviceFilter()
    {
        lock (_filterLock) _filterCache = [];
        _db.KeyDelete(_filterKey);
    }

    public PassiveCaptureSummary GetSummary()
    {
        var snapshot = Snapshot();
        IReadOnlySet<string> activeFilter;
        lock (_filterLock) activeFilter = _filterCache;

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
