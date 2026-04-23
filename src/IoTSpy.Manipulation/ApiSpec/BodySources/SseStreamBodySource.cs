using System.Text;
using IoTSpy.Core.Interfaces;

namespace IoTSpy.Manipulation.ApiSpec.BodySources;

/// <summary>
/// Replays a local <c>.sse</c> or <c>.ndjson</c> file as a Server-Sent Events stream.
/// Each event is flushed individually with an optional inter-event delay; optionally
/// loops forever until the client disconnects. Emits <c>Connection: close</c> so the
/// proxy's keep-alive loop terminates cleanly when the stream ends.
/// </summary>
public sealed class SseStreamBodySource(string filePath, int interEventDelayMs, bool loop) : IResponseBodySource
{
    public int StatusCode => 200;
    public long? ContentLength => null; // streamed; close-delimited
    public string ContentType => "text/event-stream";

    public IReadOnlyList<(string Name, string Value)> ExtraHeaders { get; } =
    [
        ("Cache-Control", "no-cache"),
        ("Connection", "close"),
        ("X-Accel-Buffering", "no"),
    ];

    public async Task WriteToAsync(Stream destination, CancellationToken ct)
    {
        var isNdjson = filePath.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase);
        var delay = Math.Max(0, interEventDelayMs);

        do
        {
            await foreach (var frame in ReadFramesAsync(filePath, isNdjson, ct))
            {
                ct.ThrowIfCancellationRequested();
                var bytes = Encoding.UTF8.GetBytes(frame);
                await destination.WriteAsync(bytes, ct);
                await destination.FlushAsync(ct);
                if (delay > 0) await Task.Delay(delay, ct);
            }
        }
        while (loop && !ct.IsCancellationRequested);

        ct.ThrowIfCancellationRequested();
    }

    private static async IAsyncEnumerable<string> ReadFramesAsync(
        string path, bool isNdjson,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using var reader = new StreamReader(path);
        if (isNdjson)
        {
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) is not null)
            {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(line)) continue;
                yield return $"data: {line}\n\n";
            }
            yield break;
        }

        // .sse: events are blank-line separated records; emit verbatim and ensure
        // trailing \n\n so each record is a well-formed event.
        var buffer = new StringBuilder();
        string? line2;
        while ((line2 = await reader.ReadLineAsync(ct)) is not null)
        {
            ct.ThrowIfCancellationRequested();
            if (line2.Length == 0)
            {
                if (buffer.Length > 0)
                {
                    yield return buffer.ToString() + "\n\n";
                    buffer.Clear();
                }
                continue;
            }
            buffer.Append(line2).Append('\n');
        }
        if (buffer.Length > 0) yield return buffer.ToString() + "\n\n";
    }
}
