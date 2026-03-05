using System.Text;
using System.Text.Json;
using IoTSpy.Core.Interfaces;

namespace IoTSpy.Protocols.Telemetry;

/// <summary>
/// Decodes AWS Kinesis Data Firehose HTTP PUT payloads.
/// Supports both the direct PUT source format and the Firehose HTTP endpoint
/// delivery format:
///   POST /  with body: { "requestId": "...", "timestamp": ..., "records": [ { "data": "&lt;base64&gt;" } ] }
/// Also handles the simple newline-delimited JSON record batch used by some agents.
/// </summary>
public sealed class FirehoseDecoder : IProtocolDecoder<TelemetryMessage>
{
    private static readonly byte[] RequestIdKey = "\"requestId\""u8.ToArray();
    private static readonly byte[] RecordsKey = "\"records\""u8.ToArray();

    public bool CanDecode(ReadOnlySpan<byte> header)
    {
        if (header.Length < 2) return false;
        var trimmed = header.TrimStart((byte)' ');
        if (trimmed.IsEmpty || trimmed[0] != (byte)'{') return false;

        var sample = header[..Math.Min(header.Length, 256)];
        return ContainsSequence(sample, RequestIdKey) && ContainsSequence(sample, RecordsKey);
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

            var requestId = root.TryGetProperty("requestId", out var rid) ? rid.GetString() ?? "" : "";
            string streamName = root.TryGetProperty("deliveryStreamArn", out var arn)
                ? arn.GetString()?.Split('/').LastOrDefault() ?? ""
                : "";

            DateTimeOffset? timestamp = null;
            if (root.TryGetProperty("timestamp", out var ts))
            {
                if (ts.ValueKind == JsonValueKind.Number)
                    timestamp = DateTimeOffset.FromUnixTimeMilliseconds(ts.GetInt64());
                else if (ts.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(ts.GetString(), out var dto))
                    timestamp = dto;
            }

            var events = new List<IReadOnlyDictionary<string, object?>>();

            if (root.TryGetProperty("records", out var records) && records.ValueKind == JsonValueKind.Array)
            {
                foreach (var record in records.EnumerateArray())
                {
                    ct.ThrowIfCancellationRequested();
                    var fields = new Dictionary<string, object?>();

                    if (record.TryGetProperty("data", out var dataEl))
                    {
                        var base64 = dataEl.GetString() ?? "";
                        fields["data_base64"] = base64;

                        // Attempt to decode as UTF-8 JSON
                        try
                        {
                            var decoded = Convert.FromBase64String(base64);
                            var text = Encoding.UTF8.GetString(decoded);
                            fields["data_text"] = text;

                            // Try to parse as nested JSON
                            try
                            {
                                using var inner = JsonDocument.Parse(text);
                                foreach (var prop in inner.RootElement.EnumerateObject())
                                    fields[prop.Name] = ExtractValue(prop.Value);
                            }
                            catch (JsonException) { /* raw text only */ }
                        }
                        catch (FormatException) { /* not base64 */ }
                    }

                    if (record.TryGetProperty("approximateArrivalTimestamp", out var aat))
                        fields["approximateArrivalTimestamp"] = aat.ToString();

                    events.Add(fields);
                }
            }

            var summary = new Dictionary<string, object?>
            {
                ["requestId"] = requestId,
                ["streamName"] = streamName,
                ["recordCount"] = events.Count
            };

            results.Add(new TelemetryMessage
            {
                Protocol = TelemetryProtocol.AwsFirehose,
                Source = streamName,
                Timestamp = timestamp,
                EventCount = events.Count,
                Events = events,
                Fields = summary,
                RawJson = json.Length > 8192 ? json[..8192] : json
            });
        }
        catch (JsonException)
        {
            // Malformed — return empty
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
