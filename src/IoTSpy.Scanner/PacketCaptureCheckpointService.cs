using IoTSpy.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Scanner;

public sealed class PacketCaptureCheckpointService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPacketBuffer _buffer;
    private readonly ILogger<PacketCaptureCheckpointService> _logger;
    private long _lastFlushedIndex;

    public PacketCaptureCheckpointService(
        IServiceScopeFactory scopeFactory,
        IPacketBuffer buffer,
        ILogger<PacketCaptureCheckpointService> logger)
    {
        _scopeFactory = scopeFactory;
        _buffer = buffer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverFromDatabaseAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await FlushAsync(stoppingToken);
        }
    }

    private async Task RecoverFromDatabaseAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IPacketRepository>();

            _lastFlushedIndex = await repo.GetMaxCaptureIndexAsync(ct);
            var recent = await repo.GetRecentAsync(_buffer.Capacity, ct);

            foreach (var pkt in recent)
                _buffer.Add(pkt);

            if (recent.Count > 0)
                _logger.LogInformation(
                    "PacketCaptureCheckpoint: recovered {Count} packets from DB (max index {Max})",
                    recent.Count, _lastFlushedIndex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "PacketCaptureCheckpoint: startup recovery failed");
        }
    }

    private async Task FlushAsync(CancellationToken ct)
    {
        var snapshot = _buffer.Snapshot();
        if (snapshot.Length == 0) return;

        // Detect capture-index reset (new session started without clearing DB watermark).
        long maxInBuffer = snapshot.Max(p => p.CaptureIndex);
        if (maxInBuffer < _lastFlushedIndex)
            _lastFlushedIndex = 0;

        var toFlush = snapshot.Where(p => p.CaptureIndex > _lastFlushedIndex).ToList();
        if (toFlush.Count == 0) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IPacketRepository>();
            await repo.AddRangeAsync(toFlush, ct);
            _lastFlushedIndex = toFlush.Max(p => p.CaptureIndex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "PacketCaptureCheckpoint: flush failed ({Count} packets)", toFlush.Count);
        }
    }
}
