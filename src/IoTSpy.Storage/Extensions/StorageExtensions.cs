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

        return services;
    }

    public static async Task MigrateAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IoTSpyDbContext>();
        await db.Database.MigrateAsync();
    }
}
