namespace IoTSpy.Core.Interfaces;

/// <summary>
/// A pluggable source of HTTP response body bytes for the proxy writer.
/// When an <see cref="Models.HttpMessage"/> carries a non-null ResponseBodySource,
/// the proxy emits the status line and headers it specifies, then delegates body
/// writing to <see cref="WriteToAsync"/>. This bypasses the string-based
/// ResponseBody path and avoids UTF-8 round-tripping for binary or streaming content.
/// </summary>
public interface IResponseBodySource
{
    /// <summary>HTTP status code to emit. 200 for full replacement, 206 for range slices.</summary>
    int StatusCode { get; }

    /// <summary>Known body length in bytes, or null for unbounded / streaming sources (e.g. SSE).</summary>
    long? ContentLength { get; }

    /// <summary>Content-Type header value to set on the outgoing response.</summary>
    string ContentType { get; }

    /// <summary>
    /// Extra headers to add or overwrite on the outgoing response
    /// (e.g. Content-Range, Accept-Ranges, Cache-Control).
    /// </summary>
    IReadOnlyList<(string Name, string Value)> ExtraHeaders { get; }

    /// <summary>
    /// Write the body to the destination stream. Implementations may block on timers
    /// (for SSE inter-event delays) and must honour the cancellation token.
    /// </summary>
    Task WriteToAsync(Stream destination, CancellationToken ct);
}
