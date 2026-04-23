using IoTSpy.Core.Interfaces;

namespace IoTSpy.Manipulation.ApiSpec.BodySources;

/// <summary>
/// Streams a file from disk into the response. Opens with async + sequential-scan
/// hints so large video/image assets never load fully into memory. Emits
/// <c>Accept-Ranges: bytes</c> so media players know to retry with a Range request.
/// </summary>
public sealed class FileStreamBodySource(string filePath, string contentType, long length) : IResponseBodySource
{
    private const int BufferSize = 64 * 1024;

    public int StatusCode => 200;
    public long? ContentLength { get; } = length;
    public string ContentType { get; } = contentType;
    public IReadOnlyList<(string Name, string Value)> ExtraHeaders { get; } =
    [
        ("Accept-Ranges", "bytes"),
    ];

    public async Task WriteToAsync(Stream destination, CancellationToken ct)
    {
        await using var fs = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await fs.CopyToAsync(destination, BufferSize, ct);
    }
}
