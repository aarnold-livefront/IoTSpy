using System.Text;
using System.Text.Json;
using IoTSpy.Core.Interfaces;

namespace IoTSpy.Protocols.Telemetry;

/// <summary>
/// Decodes Azure Monitor Log Analytics HTTP Data Collector API payloads
/// and Azure Monitor Logs Ingestion API payloads.
///
/// Data Collector (legacy): POST to /api/logs
///   Body: JSON array or single object of log entries
///   Headers carry Log-Type and x-ms-date (not decoded here, body only).
///
/// Logs Ingestion API (DCR-based): POST to /dataCollectionRules/&lt;id&gt;/streams/&lt;stream&gt;
///   Body: JSON array of log entries
///
/// Both formats share a flat-JSON-array structure, so detection is heuristic:
/// the body must be a JSON array and contain at least one object with a
/// "TimeGenerated" or "time" key.
/// </summary>
public sealed class AzureMonitorDecoder : IProtocolDecoder<TelemetryMessage>
{
    private static readonly byte[] TimeGeneratedKey = "\"TimeGenerated\""u8.ToArray();
    private static readonly byte[] TimeKey = "\"time\""u8.ToArray();
    private static readonly byte[] ResourceIdKey = "\"resourceId\""u8.ToArray();
    private static readonly byte[] CategoryKey = "\"category\""u8.ToArray();
    private static readonly byte[] OperationNameKey = "\"operationName\""u8.ToArray();

    public bool CanDecode(ReadOnlySpan<byte> header)
    {
        if (header.Length < 2) return false;

        // Azure Monitor bodies are JSON arrays '[' or objects '{'
        var trimmed = header.TrimStart((byte)' ');
        if (trimmed.IsEmpty) return false;

        var sample = header[..Math.Min(header.Length, 512)];

        // Must look like Azure Monitor: has TimeGenerated, or the combination of
        // resourceId + operationName/category (Activity/Diagnostic log format)
        if (ContainsSequence(sample, TimeGeneratedKey)) return true;

        return ContainsSequence(sample, ResourceIdKey)
            && (ContainsSequence(sample, OperationNameKey) || ContainsSequence(sample, CategoryKey));
    }

    public Task<IReadOnlyList<TelemetryMessage>> DecodeAsync(
        ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        var results = new List<TelemetryMessage>();
        var json = Encoding.UTF8.GetString(data.Span);

        try
        {
            using var doc = JsonDocument.Parse(json);

            IEnumerable<JsonElement> entries;

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                entries = doc.RootElement.EnumerateArray();
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                // Diagnostic settings export wraps entries under "records"
                if (doc.RootElement.TryGetProperty("records", out var recs) &&
                    recs.ValueKind == JsonValueKind.Array)
                    entries = recs.EnumerateArray();
                else
                    entries = [doc.RootElement];
            }
            else
            {
                return Task.FromResult<IReadOnlyList<TelemetryMessage>>(results);
            }

            var events = new List<IReadOnlyDictionary<string, object?>>();
            string source = string.Empty;
            DateTimeOffset? firstTs = null;

            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();
                var fields = new Dictionary<string, object?>();

                foreach (var prop in entry.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Object)
                    {
                        // Flatten one level deep (e.g. "properties")
                        foreach (var inner in prop.Value.EnumerateObject())
                            fields[$"{prop.Name}.{inner.Name}"] = ExtractValue(inner.Value);
                    }
                    else
                    {
                        fields[prop.Name] = ExtractValue(prop.Value);
                    }
                }

                // Extract well-known timestamp fields
                if (firstTs == null)
                {
                    foreach (var tsKey in new[] { "TimeGenerated", "time", "timestamp", "eventTimestamp" })
                    {
                        if (fields.TryGetValue(tsKey, out var tsVal) && tsVal is string tsStr &&
                            DateTimeOffset.TryParse(tsStr, out var dto))
                        {
                            firstTs = dto;
                            break;
                        }
                    }
                }

                // Extract source from resourceId or Computer
                if (string.IsNullOrEmpty(source))
                {
                    if (fields.TryGetValue("resourceId", out var rid) && rid is string ridStr)
                        source = ridStr.Split('/').LastOrDefault() ?? ridStr;
                    else if (fields.TryGetValue("Computer", out var comp) && comp is string compStr)
                        source = compStr;
                }

                events.Add(fields);
            }

            if (events.Count > 0)
            {
                results.Add(new TelemetryMessage
                {
                    Protocol = TelemetryProtocol.AzureMonitor,
                    Source = source,
                    Timestamp = firstTs,
                    EventCount = events.Count,
                    Events = events,
                    Fields = events[0],
                    RawJson = json.Length > 8192 ? json[..8192] : json
                });
            }
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
