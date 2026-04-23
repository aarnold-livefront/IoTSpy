using System.Text;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Models;
using IoTSpy.Manipulation.ApiSpec;
using IoTSpy.Manipulation.ApiSpec.BodySources;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IoTSpy.Manipulation.Tests.ApiSpec;

public class SseStreamBodySourceTests
{
    [Fact]
    public async Task Ndjson_EachLineBecomesSseDataEvent()
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".ndjson");
        try
        {
            await File.WriteAllTextAsync(path,
                "{\"msg\":\"a\"}\n{\"msg\":\"b\"}\n{\"msg\":\"c\"}\n",
                TestContext.Current.CancellationToken);

            var src = new SseStreamBodySource(path, interEventDelayMs: 0, loop: false);
            using var ms = new MemoryStream();
            await src.WriteToAsync(ms, TestContext.Current.CancellationToken);

            var output = Encoding.UTF8.GetString(ms.ToArray());
            Assert.Contains("data: {\"msg\":\"a\"}\n\n", output);
            Assert.Contains("data: {\"msg\":\"b\"}\n\n", output);
            Assert.Contains("data: {\"msg\":\"c\"}\n\n", output);
            Assert.Equal("text/event-stream", src.ContentType);
            Assert.Null(src.ContentLength);
            Assert.Contains(src.ExtraHeaders, h => h.Name == "Connection" && h.Value == "close");
            Assert.Contains(src.ExtraHeaders, h => h.Name == "Cache-Control" && h.Value == "no-cache");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SseFile_PreservesBlankLineDelimitedEvents()
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".sse");
        try
        {
            await File.WriteAllTextAsync(path,
                "event: ping\ndata: 1\n\nevent: pong\ndata: 2\n\n",
                TestContext.Current.CancellationToken);

            var src = new SseStreamBodySource(path, interEventDelayMs: 0, loop: false);
            using var ms = new MemoryStream();
            await src.WriteToAsync(ms, TestContext.Current.CancellationToken);

            var output = Encoding.UTF8.GetString(ms.ToArray());
            Assert.Contains("event: ping\ndata: 1\n\n", output);
            Assert.Contains("event: pong\ndata: 2\n\n", output);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task InterEventDelay_IsHonoured()
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".ndjson");
        try
        {
            await File.WriteAllTextAsync(path, "{\"n\":1}\n{\"n\":2}\n{\"n\":3}\n",
                TestContext.Current.CancellationToken);

            var src = new SseStreamBodySource(path, interEventDelayMs: 50, loop: false);
            using var ms = new MemoryStream();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            await src.WriteToAsync(ms, TestContext.Current.CancellationToken);
            sw.Stop();

            // Three events × 50ms ≈ 150ms floor; allow generous CI slack.
            Assert.True(sw.ElapsedMilliseconds >= 100,
                $"Expected ≥100ms elapsed, got {sw.ElapsedMilliseconds}ms");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoopMode_ReplaysUntilCancellation()
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".ndjson");
        try
        {
            await File.WriteAllTextAsync(path, "{\"x\":1}\n", TestContext.Current.CancellationToken);

            var src = new SseStreamBodySource(path, interEventDelayMs: 0, loop: true);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            cts.CancelAfter(TimeSpan.FromMilliseconds(150));
            using var ms = new MemoryStream();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await src.WriteToAsync(ms, cts.Token));

            // Looping should have emitted many copies in 150ms; assert more than one.
            var output = Encoding.UTF8.GetString(ms.ToArray());
            var count = output.Split("data: {\"x\":1}", StringSplitOptions.RemoveEmptyEntries).Length - 1;
            Assert.True(count > 1, $"Expected loop to emit event multiple times; got {count}.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ContentReplacer_MockSseStream_WiresUpBodySource()
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".ndjson");
        try
        {
            await File.WriteAllTextAsync(path, "{\"hi\":true}\n", TestContext.Current.CancellationToken);
            var replacer = new ContentReplacer(NullLogger<ContentReplacer>.Instance);
            var message = new HttpMessage
            {
                Host = "feed.example",
                Path = "/stream",
                Method = "GET",
                StatusCode = 200,
                ResponseHeaders = "Content-Type: text/event-stream",
                ResponseBody = "orig",
            };

            var rules = new List<ContentReplacementRule>
            {
                new()
                {
                    Enabled = true,
                    MatchType = ContentMatchType.ContentType,
                    MatchPattern = "text/event-stream",
                    Action = ContentReplacementAction.MockSseStream,
                    ReplacementFilePath = path,
                    SseInterEventDelayMs = 0,
                    SseLoop = false,
                },
            };

            var modified = await replacer.ApplyAsync(message, rules, TestContext.Current.CancellationToken);

            Assert.True(modified);
            Assert.IsType<SseStreamBodySource>(message.ResponseBodySource);
            Assert.Equal("text/event-stream", message.ResponseBodySource!.ContentType);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
