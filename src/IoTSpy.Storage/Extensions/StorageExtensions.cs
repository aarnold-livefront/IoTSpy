using IoTSpy.Core.Interfaces;
using IoTSpy.Storage.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IoTSpy.Storage.Extensions;

public static class StorageExtensions
{
    public static IServiceCollection AddIoTSpyStorage(
        this IServiceCollection services,
        string connectionString,
        string provider = "sqlite")
    {
        services.AddDbContext<IoTSpyDbContext>(opts =>
        {
            if (provider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                opts.UseNpgsql(connectionString);
            else
                opts.UseSqlite(connectionString);
        });

        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<ICaptureRepository, CaptureRepository>();
        services.AddScoped<ICertificateRepository, CertificateRepository>();
        services.AddScoped<IProxySettingsRepository, ProxySettingsRepository>();
        services.AddScoped<IScanJobRepository, ScanJobRepository>();
        services.AddScoped<IManipulationRuleRepository, ManipulationRuleRepository>();
        services.AddScoped<IBreakpointRepository, BreakpointRepository>();
        services.AddScoped<IReplaySessionRepository, ReplaySessionRepository>();
        services.AddScoped<IFuzzerJobRepository, FuzzerJobRepository>();

        return services;
    }

    public static async Task MigrateAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IoTSpyDbContext>();
        await db.Database.MigrateAsync();
    }
}
