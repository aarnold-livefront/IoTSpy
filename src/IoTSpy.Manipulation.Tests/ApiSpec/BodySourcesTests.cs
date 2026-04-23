using System.Text;
using IoTSpy.Manipulation.ApiSpec.BodySources;
using Xunit;

namespace IoTSpy.Manipulation.Tests.ApiSpec;

public class BodySourcesTests
{
    [Fact]
    public async Task TrackingPixel_Writes43Bytes_WithGifMagic()
    {
        var src = TrackingPixelBodySource.Instance;
        using var ms = new MemoryStream();

        await src.WriteToAsync(ms, TestContext.Current.CancellationToken);

        var bytes = ms.ToArray();
        Assert.Equal(43, bytes.Length);
        Assert.Equal(43, src.ContentLength);
        Assert.Equal("image/gif", src.ContentType);
        Assert.Equal("GIF89a", Encoding.ASCII.GetString(bytes, 0, 6));
        Assert.Equal(0x3B, bytes[^1]); // GIF trailer
        Assert.Contains(src.ExtraHeaders, h => h.Name == "Cache-Control" && h.Value == "no-store");
    }

    [Fact]
    public async Task FileStreamBodySource_StreamsFileBytesVerbatim()
    {
        var payload = new byte[4096];
        new Random(42).NextBytes(payload);
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(path, payload, TestContext.Current.CancellationToken);
            var src = new FileStreamBodySource(path, "image/png", payload.Length);

            using var ms = new MemoryStream();
            await src.WriteToAsync(ms, TestContext.Current.CancellationToken);

            Assert.Equal(payload, ms.ToArray());
            Assert.Equal(200, src.StatusCode);
            Assert.Equal(payload.Length, src.ContentLength);
            Assert.Contains(src.ExtraHeaders, h => h.Name == "Accept-Ranges" && h.Value == "bytes");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RangeSlicedBodySource_Serves206_WithCorrectContentRangeAndBytes()
    {
        var payload = new byte[1000];
        for (var i = 0; i < payload.Length; i++) payload[i] = (byte)(i % 256);
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(path, payload, TestContext.Current.CancellationToken);
            var src = new RangeSlicedBodySource(path, start: 100, end: 299, totalLength: 1000, contentType: "video/mp4");

            using var ms = new MemoryStream();
            await src.WriteToAsync(ms, TestContext.Current.CancellationToken);

            Assert.Equal(206, src.StatusCode);
            Assert.Equal(200, src.ContentLength);
            Assert.Equal(payload[100..300], ms.ToArray());
            Assert.Contains(src.ExtraHeaders, h => h.Name == "Content-Range" && h.Value == "bytes 100-299/1000");
            Assert.Contains(src.ExtraHeaders, h => h.Name == "Accept-Ranges" && h.Value == "bytes");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ByteArrayBodySource_WritesExactBytes()
    {
        var payload = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var src = new ByteArrayBodySource(payload, "application/octet-stream");

        using var ms = new MemoryStream();
        await src.WriteToAsync(ms, TestContext.Current.CancellationToken);

        Assert.Equal(payload, ms.ToArray());
        Assert.Equal(4, src.ContentLength);
        Assert.Equal("application/octet-stream", src.ContentType);
    }
}
