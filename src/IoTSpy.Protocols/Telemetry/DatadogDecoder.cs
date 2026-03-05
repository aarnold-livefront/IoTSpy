using System.Text;
using System.Text.Json;
using IoTSpy.Core.Interfaces;

namespace IoTSpy.Protocols.Telemetry;

/// <summary>
/// Decodes Datadog Agent metric/log payloads.
/// Supports the v1 series API (POST /api/v1/series) and v2 metrics intake
/// (POST /api/v2/series) JSON formats.
/// </summary>
public sealed class DatadogDecoder : IProtocolDecoder<TelemetryMessage>
{
    // Datadog JSON bodies always start with '{"series"' or '{"metrics"' etc.
    private static readonly byte[] SeriesPrefix = "\"series\""u8.ToArray();
    private static readonly byte[] MetricPrefix = "\"metrics\""u8.ToArray();
    private static readonly byte[] LogsPrefix = "\"logs\""u8.ToArray();

    public bool CanDecode(ReadOnlySpan<byte> header)
    {
        if (header.Length < 2) return false;

        // Must start with '{'
        var trimmed = header.TrimStart((byte)' ');
        if (trimmed.IsEmpty || trimmed[0] != (byte)'{') return false;

        // Look for known Datadog payload keys within first 256 bytes
        var sample = header[..Math.Min(header.Length, 256)];
        return ContainsSequence(sample, SeriesPrefix)
            || ContainsSequence(sample, MetricPrefix)
            || ContainsSequence(sample, LogsPrefix);
    }

    public Task<IReadOnlyList<TelemetryMessage>> DecodeAsync(
        ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        var results = new List<TelemetryMessage>();
        var json = Encoding.UTF8.GetString(data.Span);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // v1 series: { "series": [ { "metric": "...", "points": [...], "tags": [...], "host": "..." } ] }
            // v2 series: { "series": [ { "metric": "...", "points": [...], "resources": [...] } ] }
            if (root.TryGetProperty("series", out var series) && series.ValueKind == JsonValueKind.Array)
            {
                var events = new List<IReadOnlyDictionary<string, object?>>();
                string source = string.Empty;
                DateTimeOffset? firstTs = null;

                foreach (var item in series.EnumerateArray())
                {
                    ct.ThrowIfCancellationRequested();
                    var fields = new Dictionary<string, object?>();

                    if (item.TryGetProperty("metric", out var metric))
                        fields["metric"] = metric.GetString();

                    if (item.TryGetProperty("host", out var host))
                    {
                        fields["host"] = host.GetString();
                        if (string.IsNullOrEmpty(source)) source = host.GetString() ?? string.Empty;
                    }

                    if (item.TryGetProperty("type", out var type))
                        fields["type"] = type.GetString();

                    if (item.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
                        fields["tags"] = string.Join(",", tags.EnumerateArray().Select(t => t.GetString()));

                    if (item.TryGetProperty("points", out var points) && points.ValueKind == JsonValueKind.Array)
                    {
                        // Each point is [timestamp, value] in v1, or {timestamp, value} in v2
                        foreach (var pt in points.EnumerateArray())
                        {
                            if (pt.ValueKind == JsonValueKind.Array)
                            {
                                var arr = pt.EnumerateArray().ToList();
                                if (arr.Count >= 2)
                                {
                                    var ts = arr[0].GetInt64();
                                    firstTs ??= DateTimeOffset.FromUnixTimeSeconds(ts);
                                    fields["value"] = arr[1].GetDouble();
                                    fields["timestamp"] = ts;
                                }
                            }
                        }
                    }

                    events.Add(fields);
                }

                results.Add(new TelemetryMessage
                {
                    Protocol = TelemetryProtocol.Datadog,
                    Source = source,
                    Timestamp = firstTs,
                    EventCount = events.Count,
                    Events = events,
                    Fields = events.FirstOrDefault() ?? new Dictionary<string, object?>(),
                    RawJson = json.Length > 8192 ? json[..8192] : json
                });
            }
            // Logs: { "logs": [ { "message": "...", "timestamp": "...", "hostname": "...", "service": "..." } ] }
            else if (root.TryGetProperty("logs", out var logs) && logs.ValueKind == JsonValueKind.Array)
            {
                var events = new List<IReadOnlyDictionary<string, object?>>();
                string source = string.Empty;
                DateTimeOffset? firstTs = null;

                foreach (var item in logs.EnumerateArray())
                {
                    ct.ThrowIfCancellationRequested();
                    var fields = new Dictionary<string, object?>();

                    if (item.TryGetProperty("message", out var msg)) fields["message"] = msg.GetString();
                    if (item.TryGetProperty("hostname", out var hn))
                    {
                        fields["hostname"] = hn.GetString();
                        if (string.IsNullOrEmpty(source)) source = hn.GetString() ?? string.Empty;
                    }
                    if (item.TryGetProperty("service", out var svc)) fields["service"] = svc.GetString();
                    if (item.TryGetProperty("timestamp", out var ts))
                    {
                        fields["timestamp"] = ts.ToString();
                        if (DateTimeOffset.TryParse(ts.GetString(), out var dto))
                            firstTs ??= dto;
                    }

                    events.Add(fields);
                }

                results.Add(new TelemetryMessage
                {
                    Protocol = TelemetryProtocol.Datadog,
                    Source = source,
                    Timestamp = firstTs,
                    EventCount = events.Count,
                    Events = events,
                    Fields = events.FirstOrDefault() ?? new Dictionary<string, object?>(),
                    RawJson = json.Length > 8192 ? json[..8192] : json
                });
            }
        }
        catch (JsonException)
        {
            // Malformed JSON — return empty
        }

        return Task.FromResult<IReadOnlyList<TelemetryMessage>>(results);
    }

    private static bool ContainsSequence(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        return haystack.IndexOf(needle) >= 0;
    }
}
