using System.Text.Json;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Models;
using IoTSpy.Manipulation.ApiSpec;
using IoTSpy.Manipulation.ApiSpec.BodySources;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IoTSpy.Manipulation.Tests.ApiSpec;

/// <summary>
/// Covers the Phase 22 binary-safe replacement pipeline: PNG/MP4/WebM round-trip
/// without base64 corruption, HTTP range slicing, and the built-in tracking pixel.
/// </summary>
public class ContentReplacerBinaryTests
{
    private static ContentReplacer CreateReplacer() => new(NullLogger<ContentReplacer>.Instance);

    private static HttpMessage MakeImageResponse(string contentType = "image/jpeg", string? rangeHeader = null)
    {
        var reqHeaders = rangeHeader is null
            ? string.Empty
            : JsonSerializer.Serialize(new Dictionary<string, string> { ["Range"] = rangeHeader });

        return new HttpMessage
        {
            Host = "cdn.example.com",
            Path = "/ads/banner.jpg",
            Method = "GET",
            StatusCode = 200,
            RequestHeaders = reqHeaders,
            ResponseHeaders = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["Content-Type"] = contentType,
            }),
            ResponseBody = "original-upstream-body",
        };
    }

    [Fact]
    public async Task ReplaceWithFile_BinaryImage_SetsResponseBodySource_NotBase64String()
    {
        var replacer = CreateReplacer();
        var pngMagic = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0xDE, 0xAD };
        var path = Path.GetTempFileName();
        var renamed = Path.ChangeExtension(path, ".png");
        File.Move(path, renamed);
        try
        {
            await File.WriteAllBytesAsync(renamed, pngMagic, TestContext.Current.CancellationToken);

            var message = MakeImageResponse();
            var rules = new List<ContentReplacementRule>
            {
                new()
                {
                    Enabled = true,
                    MatchType = ContentMatchType.ContentType,
                    MatchPattern = "image/*",
                    Action = ContentReplacementAction.ReplaceWithFile,
                    ReplacementFilePath = renamed,
                },
            };

            var modified = await replacer.ApplyAsync(message, rules, TestContext.Current.CancellationToken);

            Assert.True(modified);
            Assert.NotNull(message.ResponseBodySource);
            Assert.IsType<FileStreamBodySource>(message.ResponseBodySource);
            Assert.Equal("image/png", message.ResponseBodySource!.ContentType);
            Assert.Equal(pngMagic.Length, message.ResponseBodySource.ContentLength);

            // Critical: the string body must NOT be populated with base64 text
            // (that was the pre-Phase-22 bug — clients received base64 strings on the wire).
            Assert.DoesNotContain(Convert.ToBase64String(pngMagic), message.ResponseBody);

            // And the actual bytes must round-trip through WriteToAsync.
            using var ms = new MemoryStream();
            await message.ResponseBodySource.WriteToAsync(ms, TestContext.Current.CancellationToken);
            Assert.Equal(pngMagic, ms.ToArray());
        }
        finally
        {
            if (File.Exists(renamed)) File.Delete(renamed);
        }
    }

    [Fact]
    public async Task ReplaceWithFile_WithRangeHeader_EmitsRangeSlicedBodySource()
    {
        var replacer = CreateReplacer();
        var bytes = new byte[1024];
        for (var i = 0; i < bytes.Length; i++) bytes[i] = (byte)(i & 0xFF);
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".mp4");
        try
        {
            await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);

            var message = MakeImageResponse(contentType: "video/mp4", rangeHeader: "bytes=100-199");
            var rules = new List<ContentReplacementRule>
            {
                new()
                {
                    Enabled = true,
                    MatchType = ContentMatchType.ContentType,
                    MatchPattern = "video/*",
                    Action = ContentReplacementAction.ReplaceWithFile,
                    ReplacementFilePath = path,
                },
            };

            var modified = await replacer.ApplyAsync(message, rules, TestContext.Current.CancellationToken);

            Assert.True(modified);
            Assert.IsType<RangeSlicedBodySource>(message.ResponseBodySource);
            Assert.Equal(206, message.ResponseBodySource!.StatusCode);
            Assert.Equal(100, message.ResponseBodySource.ContentLength);
            Assert.Equal(206, message.StatusCode);
            Assert.Contains(message.ResponseBodySource.ExtraHeaders,
                h => h.Name == "Content-Range" && h.Value == "bytes 100-199/1024");

            using var ms = new MemoryStream();
            await message.ResponseBodySource.WriteToAsync(ms, TestContext.Current.CancellationToken);
            Assert.Equal(bytes[100..200], ms.ToArray());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ReplaceWithFile_InvalidRangeHeader_FallsBackToFullFile()
    {
        var replacer = CreateReplacer();
        var bytes = new byte[256];
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".webp");
        try
        {
            await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);

            var message = MakeImageResponse(contentType: "image/webp", rangeHeader: "bytes=abc-def");
            var rules = new List<ContentReplacementRule>
            {
                new()
                {
                    Enabled = true,
                    MatchType = ContentMatchType.ContentType,
                    MatchPattern = "image/*",
                    Action = ContentReplacementAction.ReplaceWithFile,
                    ReplacementFilePath = path,
                },
            };

            var modified = await replacer.ApplyAsync(message, rules, TestContext.Current.CancellationToken);

            Assert.True(modified);
            Assert.IsType<FileStreamBodySource>(message.ResponseBodySource);
            Assert.Equal(200, message.ResponseBodySource!.StatusCode);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ReplaceWithFile_TextFile_KeepsLegacyStringPath()
    {
        var replacer = CreateReplacer();
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".html");
        try
        {
            await File.WriteAllTextAsync(path, "<h1>Replaced</h1>", TestContext.Current.CancellationToken);

            var message = MakeImageResponse(contentType: "text/html");
            var rules = new List<ContentReplacementRule>
            {
                new()
                {
                    Enabled = true,
                    MatchType = ContentMatchType.ContentType,
                    MatchPattern = "text/*",
                    Action = ContentReplacementAction.ReplaceWithFile,
                    ReplacementFilePath = path,
                },
            };

            var modified = await replacer.ApplyAsync(message, rules, TestContext.Current.CancellationToken);

            Assert.True(modified);
            Assert.Null(message.ResponseBodySource);
            Assert.Equal("<h1>Replaced</h1>", message.ResponseBody);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task TrackingPixel_Action_SetsSingletonBodySource()
    {
        var replacer = CreateReplacer();
        var message = MakeImageResponse(contentType: "image/gif");
        var rules = new List<ContentReplacementRule>
        {
            new()
            {
                Enabled = true,
                MatchType = ContentMatchType.ContentType,
                MatchPattern = "image/*",
                Action = ContentReplacementAction.TrackingPixel,
            },
        };

        var modified = await replacer.ApplyAsync(message, rules, TestContext.Current.CancellationToken);

        Assert.True(modified);
        Assert.Same(TrackingPixelBodySource.Instance, message.ResponseBodySource);
    }

    [Fact]
    public async Task ContentTypeRule_MatchesCrlfHeaders_FromRealProxyTraffic()
    {
        // Regression: ContentReplacer used to assume headers were always JSON-dict form,
        // silently failing against real proxy traffic which uses CRLF-separated lines.
        var replacer = CreateReplacer();
        var message = new HttpMessage
        {
            Host = "a.example",
            Path = "/img",
            Method = "GET",
            StatusCode = 200,
            RequestHeaders = string.Empty,
            ResponseHeaders = "Content-Type: image/jpeg\r\nServer: nginx\r\nCache-Control: max-age=0",
            ResponseBody = "upstream",
        };

        var path = Path.ChangeExtension(Path.GetTempFileName(), ".jpg");
        try
        {
            await File.WriteAllBytesAsync(path, new byte[] { 0xFF, 0xD8, 0xFF }, TestContext.Current.CancellationToken);
            var rules = new List<ContentReplacementRule>
            {
                new()
                {
                    Enabled = true,
                    MatchType = ContentMatchType.ContentType,
                    MatchPattern = "image/*",
                    Action = ContentReplacementAction.ReplaceWithFile,
                    ReplacementFilePath = path,
                },
            };

            var modified = await replacer.ApplyAsync(message, rules, TestContext.Current.CancellationToken);

            Assert.True(modified);
            Assert.NotNull(message.ResponseBodySource);
            Assert.Contains("Content-Type: image/jpeg", message.ResponseHeaders);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
