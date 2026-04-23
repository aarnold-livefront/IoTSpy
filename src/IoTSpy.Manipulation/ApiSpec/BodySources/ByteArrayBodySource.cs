using IoTSpy.Core.Interfaces;

namespace IoTSpy.Manipulation.ApiSpec.BodySources;

/// <summary>
/// In-memory response body source. Useful for small fixed payloads and tests.
/// Prefer <see cref="FileStreamBodySource"/> for arbitrary-size file replacements
/// so large files don't sit in memory.
/// </summary>
public sealed class ByteArrayBodySource(
    ReadOnlyMemory<byte> bytes,
    string contentType,
    int statusCode = 200,
    IReadOnlyList<(string Name, string Value)>? extraHeaders = null) : IResponseBodySource
{
    public int StatusCode { get; } = statusCode;
    public long? ContentLength => bytes.Length;
    public string ContentType { get; } = contentType;
    public IReadOnlyList<(string Name, string Value)> ExtraHeaders { get; } = extraHeaders ?? [];

    public async Task WriteToAsync(Stream destination, CancellationToken ct)
    {
        await destination.WriteAsync(bytes, ct);
    }
}
