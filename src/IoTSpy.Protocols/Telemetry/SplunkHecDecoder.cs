using System.Text;
using System.Text.Json;
using IoTSpy.Core.Interfaces;

namespace IoTSpy.Protocols.Telemetry;

/// <summary>
/// Decodes Splunk HTTP Event Collector (HEC) payloads.
/// Supports both single-event JSON objects and newline-delimited JSON batches:
///   Single:  { "time": 1234567890, "host": "...", "source": "...", "event": { ... } }
///   Batch:   { ... }\n{ ... }\n...
/// </summary>
public sealed class SplunkHecDecoder : IProtocolDecoder<TelemetryMessage>
{
    private static readonly byte[] EventKey = "\"event\""u8.ToArray();
    private static readonly byte[] HostKey = "\"host\""u8.ToArray();
    private static readonly byte[] SourcetypeKey = "\"sourcetype\""u8.ToArray();
    private static readonly byte[] IndexKey = "\"index\""u8.ToArray();

    public bool CanDecode(ReadOnlySpan<byte> header)
    {
        if (header.Length < 2) return false;
        var trimmed = header.TrimStart((byte)' ');
        if (trimmed.IsEmpty || trimmed[0] != (byte)'{') return false;

        var sample = header[..Math.Min(header.Length, 256)];
        // Splunk HEC events always carry an "event" key, typically alongside "host" or "sourcetype"
        return ContainsSequence(sample, EventKey)
            && (ContainsSequence(sample, HostKey)
                || ContainsSequence(sample, SourcetypeKey)
                || ContainsSequence(sample, IndexKey));
    }

    public Task<IReadOnlyList<TelemetryMessage>> DecodeAsync(
        ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        var results = new List<TelemetryMessage>();
        var body = Encoding.UTF8.GetString(data.Span);

        // Split on newlines to handle batched events
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var events = new List<IReadOnlyDictionary<string, object?>>();
        string source = string.Empty;
        DateTimeOffset? firstTs = null;

        foreach (var line in lines)
        {
            ct.ThrowIfCancellationRequested();
            if (!line.StartsWith('{')) continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                var fields = new Dictionary<string, object?>();

                // Splunk HEC top-level fields
                if (root.TryGetProperty("time", out var time))
                {
                    double epochSec = time.ValueKind == JsonValueKind.Number ? time.GetDouble() : 0;
                    if (epochSec > 0)
                    {
                        var ts = DateTimeOffset.FromUnixTimeMilliseconds((long)(epochSec * 1000));
                        firstTs ??= ts;
                        fields["time"] = epochSec;
                    }
                }

                if (root.TryGetProperty("host", out var host))
                {
                    fields["host"] = host.GetString();
                    if (string.IsNullOrEmpty(source)) source = host.GetString() ?? string.Empty;
                }

                if (root.TryGetProperty("source", out var src))
                    fields["source"] = src.GetString();

                if (root.TryGetProperty("sourcetype", out var st))
                    fields["sourcetype"] = st.GetString();

                if (root.TryGetProperty("index", out var idx))
                    fields["index"] = idx.GetString();

                // Flatten the nested "event" object / string
                if (root.TryGetProperty("event", out var evt))
                {
                    if (evt.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in evt.EnumerateObject())
                            fields[$"event.{prop.Name}"] = ExtractValue(prop.Value);
                    }
                    else
                    {
                        fields["event"] = ExtractValue(evt);
                    }
                }

                // Flatten optional "fields" object (HEC metadata fields)
                if (root.TryGetProperty("fields", out var metaFields) && metaFields.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in metaFields.EnumerateObject())
                        fields[$"fields.{prop.Name}"] = ExtractValue(prop.Value);
                }

                events.Add(fields);
            }
            catch (JsonException) { /* skip malformed line */ }
        }

        if (events.Count > 0)
        {
            results.Add(new TelemetryMessage
            {
                Protocol = TelemetryProtocol.SplunkHec,
                Source = source,
                Timestamp = firstTs,
                EventCount = events.Count,
                Events = events,
                Fields = events[0],
                RawJson = body.Length > 8192 ? body[..8192] : body
            });
        }

        return Task.FromResult<IReadOnlyList<TelemetryMessage>>(results);
    }

    private static object? ExtractValue(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var i) ? i : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => el.ToString()
    };

    private static bool ContainsSequence(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
        => haystack.IndexOf(needle) >= 0;
}
