using IoTSpy.Api.Services;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IoTSpy.Api.Tests;

public class CaptureBatchWriterTests
{
    private static CapturedRequest MakeCapture(string host = "example.com") =>
        new() { Host = host, Method = "GET", Path = "/", StatusCode = 200, ClientIp = "10.0.0.1" };

    private static (CaptureBatchWriter writer, ICaptureRepository repo, ICapturePublisher publisher) Build()
    {
        var repo = Substitute.For<ICaptureRepository>();
        repo.AddBatchAsync(Arg.Any<IReadOnlyList<CapturedRequest>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var publisher = Substitute.For<ICapturePublisher>();
        publisher.PublishAsync(Arg.Any<CapturedRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        var sp = Substitute.For<IServiceProvider>();
        sp.GetService(typeof(ICaptureRepository)).Returns(repo);
        scope.ServiceProvider.Returns(sp);
        scopeFactory.CreateScope().Returns(scope);

        var writer = new CaptureBatchWriter(scopeFactory, publisher, NullLogger<CaptureBatchWriter>.Instance);
        return (writer, repo, publisher);
    }

    [Fact]
    public void TryEnqueue_ReturnsTrue_WhenChannelNotFull()
    {
        var (writer, _, _) = Build();
        Assert.True(writer.TryEnqueue(MakeCapture()));
    }

    [Fact]
    public async Task ExecuteAsync_PersistsEnqueuedCaptures()
    {
        var (writer, repo, _) = Build();

        writer.TryEnqueue(MakeCapture("a.com"));
        writer.TryEnqueue(MakeCapture("b.com"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var task = writer.StartAsync(cts.Token);

        // Give the consumer time to flush
        await Task.Delay(500, CancellationToken.None);
        cts.Cancel();
        try { await task; } catch (OperationCanceledException) { }

        await repo.Received().AddBatchAsync(
            Arg.Is<IReadOnlyList<CapturedRequest>>(b => b.Count >= 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_PublishesEachCaptureViaSignalR()
    {
        var (writer, _, publisher) = Build();

        writer.TryEnqueue(MakeCapture("x.com"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var task = writer.StartAsync(cts.Token);

        await Task.Delay(500, CancellationToken.None);
        cts.Cancel();
        try { await task; } catch (OperationCanceledException) { }

        await publisher.Received().PublishAsync(
            Arg.Is<CapturedRequest>(c => c.Host == "x.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_StillPublishes_WhenPersistFails()
    {
        var (writer, repo, publisher) = Build();

        repo.AddBatchAsync(Arg.Any<IReadOnlyList<CapturedRequest>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("DB down")));

        writer.TryEnqueue(MakeCapture("fail.com"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var task = writer.StartAsync(cts.Token);

        await Task.Delay(500, CancellationToken.None);
        cts.Cancel();
        try { await task; } catch (OperationCanceledException) { }

        // SignalR publish should still be called even when DB insert throws.
        await publisher.Received().PublishAsync(
            Arg.Any<CapturedRequest>(),
            Arg.Any<CancellationToken>());
    }
}
