using IoTSpy.Core.Interfaces;

namespace IoTSpy.Manipulation.ApiSpec.BodySources;

/// <summary>
/// Singleton that serves a hard-coded 43-byte 1x1 transparent GIF89a. Useful for
/// silently neutralising tracking beacons without requiring a file upload.
/// </summary>
public sealed class TrackingPixelBodySource : IResponseBodySource
{
    public static TrackingPixelBodySource Instance { get; } = new();

    // Standard 1x1 transparent GIF89a. 43 bytes.
    private static readonly byte[] Pixel =
    [
        0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00,
        0x01, 0x00, 0x80, 0x00, 0x00, 0xFF, 0xFF, 0xFF,
        0x00, 0x00, 0x00, 0x21, 0xF9, 0x04, 0x01, 0x00,
        0x00, 0x00, 0x00, 0x2C, 0x00, 0x00, 0x00, 0x00,
        0x01, 0x00, 0x01, 0x00, 0x00, 0x02, 0x02, 0x44,
        0x01, 0x00, 0x3B,
    ];

    private TrackingPixelBodySource() { }

    public int StatusCode => 200;
    public long? ContentLength => Pixel.Length;
    public string ContentType => "image/gif";
    public IReadOnlyList<(string Name, string Value)> ExtraHeaders { get; } =
    [
        ("Cache-Control", "no-store"),
    ];

    public Task WriteToAsync(Stream destination, CancellationToken ct) =>
        destination.WriteAsync(Pixel, ct).AsTask();
}
