using System.Text.Json;
using System.Text.Json.Serialization;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Threading.Channels;

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
///
/// Hot-path note: <see cref="Add"/> is non-blocking — it writes to an internal
/// bounded Channel; a background consumer batches writes into a single Redis
/// pipeline per flush cycle, eliminating per-request network round-trips.
///
/// Filter-cache note: <see cref="DeviceFilter"/> is an in-process cache.
/// Changes via <see cref="SetDeviceFilter"/> on one API instance are not
/// automatically propagated to others; a Redis pub/sub mechanism would be
/// required for full multi-instance filter sync.
/// </summary>
public sealed class RedisPassiveProxyBuffer : BackgroundService, IPassiveProxyBuffer
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
    private readonly ILogger<RedisPassiveProxyBuffer> _logger;
    private readonly Channel<CapturedRequest> _writeChannel;

    private HashSet<string> _filterCache = [];
    private readonly Lock _filterLock = new();

    private const int FlushBatchSize = 50;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(100);

    public RedisPassiveProxyBuffer(
        IConnectionMultiplexer redis,
        RedisOptions options,
        ILogger<RedisPassiveProxyBuffer> logger)
    {
        _db = redis.GetDatabase();
        _bufferKey = $"{options.KeyPrefix}:passive:buffer";
        _filterKey = $"{options.KeyPrefix}:passive:filter";
        _maxCapacity = options.PassiveBufferCapacity;
        _logger = logger;

        // Capacity mirrors the buffer capacity — the channel acts as the in-flight queue.
        _writeChannel = Channel.CreateBounded<CapturedRequest>(
            new BoundedChannelOptions(_maxCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
    }

    // Hydrate the local filter cache from Redis before the consume loop starts,
    // so Add() sees the correct filter state from the first request.
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var members = await _db.SetMembersAsync(_filterKey);
            lock (_filterLock)
                _filterCache = new HashSet<string>(
                    members.Select(m => m.ToString()),
                    StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load device filter from Redis on startup; starting in capture-all mode");
        }
        await base.StartAsync(cancellationToken);
    }

    // Non-blocking hot path: filter check is in-memory; channel write is lock-free.
    public void Add(CapturedRequest capture)
    {
        HashSet<string> filter;
        lock (_filterLock) filter = _filterCache;
        if (filter.Count > 0 && !filter.Contains(capture.ClientIp)) return;
        _writeChannel.Writer.TryWrite(capture);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<CapturedRequest>(FlushBatchSize);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var flushCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                flushCts.CancelAfter(FlushInterval);
                try { await _writeChannel.Reader.WaitToReadAsync(flushCts.Token); }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested) { }

                while (batch.Count < FlushBatchSize && _writeChannel.Reader.TryRead(out var item))
                    batch.Add(item);

                if (batch.Count > 0)
                {
                    PersistBatch(batch);
                    batch.Clear();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis passive buffer consumer encountered an error; retrying after delay");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    // Sends all captures in a single Redis pipeline round-trip (RPUSH × N + LTRIM).
    private void PersistBatch(List<CapturedRequest> batch)
    {
        try
        {
            var redisBatch = _db.CreateBatch();
            foreach (var capture in batch)
                _ = redisBatch.ListRightPushAsync(_bufferKey, JsonSerializer.Serialize(capture, _json));
            _ = redisBatch.ListTrimAsync(_bufferKey, -_maxCapacity, -1);
            redisBatch.Execute();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist batch of {Count} passive captures to Redis", batch.Count);
        }
    }

    public int Count => (int)_db.ListLength(_bufferKey);

    public IReadOnlySet<string> DeviceFilter
    {
        get { lock (_filterLock) return _filterCache; }
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

        // Write to a temp key then rename atomically so a concurrent reader on
        // another instance never sees an empty filter between Delete and SetAdd.
        if (set.Count > 0)
        {
            var tempKey = new RedisKey($"{_filterKey}:tmp");
            _db.KeyDelete(tempKey);
            _db.SetAdd(tempKey, set.Select(ip => (RedisValue)ip).ToArray());
            _db.KeyRename(tempKey, _filterKey);
        }
        else
        {
            _db.KeyDelete(_filterKey);
        }
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
