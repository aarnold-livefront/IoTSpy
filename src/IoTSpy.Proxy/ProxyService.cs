using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using IoTSpy.Proxy.Interception;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Proxy;

public class ProxyService(
    ExplicitProxyServer explicitProxy,
    IServiceScopeFactory scopeFactory,
    ILogger<ProxyService> logger) : IProxyService, IHostedService
{
    private ProxySettings? _settings;

    public bool IsRunning => explicitProxy.IsRunning;
    public int Port => _settings?.ProxyPort ?? 8888;

    public ProxySettings GetSettings() => _settings ?? new ProxySettings();

    // Called by POST /api/proxy/start — always starts and persists IsRunning=true
    public async Task StartAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var settingsRepo = scope.ServiceProvider.GetRequiredService<IProxySettingsRepository>();
        _settings = await settingsRepo.GetAsync(ct);
        _settings.IsRunning = true;
        await settingsRepo.SaveAsync(_settings, ct);
        await explicitProxy.StartAsync(_settings.ProxyPort, _settings.ListenAddress, ct);
        logger.LogInformation("ProxyService started on port {Port}", _settings.ProxyPort);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        await explicitProxy.StopAsync();
        if (_settings is not null)
        {
            _settings.IsRunning = false;
            using var scope = scopeFactory.CreateScope();
            var settingsRepo = scope.ServiceProvider.GetRequiredService<IProxySettingsRepository>();
            await settingsRepo.SaveAsync(_settings, ct);
        }
    }

    public async Task UpdateSettingsAsync(ProxySettings settings, CancellationToken ct = default)
    {
        var wasRunning = explicitProxy.IsRunning;
        if (wasRunning) await explicitProxy.StopAsync();

        using var scope = scopeFactory.CreateScope();
        var settingsRepo = scope.ServiceProvider.GetRequiredService<IProxySettingsRepository>();
        _settings = await settingsRepo.SaveAsync(settings, ct);

        if (settings.IsRunning)
            await explicitProxy.StartAsync(settings.ProxyPort, settings.ListenAddress, ct);
    }

    // IHostedService: at app boot, only auto-start if DB says IsRunning=true
    async Task IHostedService.StartAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var settingsRepo = scope.ServiceProvider.GetRequiredService<IProxySettingsRepository>();
        _settings = await settingsRepo.GetAsync(ct);
        if (_settings.IsRunning)
        {
            await explicitProxy.StartAsync(_settings.ProxyPort, _settings.ListenAddress, ct);
            logger.LogInformation("ProxyService auto-started on port {Port}", _settings.ProxyPort);
        }
    }

    Task IHostedService.StopAsync(CancellationToken ct) => StopAsync(ct);
}
