using System.Globalization;
using IoTSpy.Core.Interfaces;

namespace IoTSpy.Manipulation.ApiSpec.BodySources;

/// <summary>
/// Serves a byte range from a file as an HTTP 206 Partial Content response,
/// emitting the required Content-Range and Accept-Ranges headers. Required by
/// <c>&lt;video&gt;</c> elements and most native media players to scrub through assets.
/// </summary>
public sealed class RangeSlicedBodySource : IResponseBodySource
{
    private const int BufferSize = 64 * 1024;

    private readonly string _filePath;
    private readonly long _start;
    private readonly long _end;
    private readonly long _totalLength;

    public RangeSlicedBodySource(string filePath, long start, long end, long totalLength, string contentType)
    {
        _filePath = filePath;
        _start = start;
        _end = end;
        _totalLength = totalLength;
        ContentType = contentType;
        ExtraHeaders =
        [
            ("Content-Range", string.Create(CultureInfo.InvariantCulture, $"bytes {start}-{end}/{totalLength}")),
            ("Accept-Ranges", "bytes"),
        ];
    }

    public int StatusCode => 206;
    public long? ContentLength => _end - _start + 1;
    public string ContentType { get; }
    public IReadOnlyList<(string Name, string Value)> ExtraHeaders { get; }

    public async Task WriteToAsync(Stream destination, CancellationToken ct)
    {
        await using var fs = new FileStream(
            _filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        fs.Seek(_start, SeekOrigin.Begin);

        var remaining = _end - _start + 1;
        var buffer = new byte[BufferSize];
        while (remaining > 0)
        {
            var toRead = (int)Math.Min(buffer.Length, remaining);
            var read = await fs.ReadAsync(buffer.AsMemory(0, toRead), ct);
            if (read <= 0) break;
            await destination.WriteAsync(buffer.AsMemory(0, read), ct);
            remaining -= read;
        }
    }
}
