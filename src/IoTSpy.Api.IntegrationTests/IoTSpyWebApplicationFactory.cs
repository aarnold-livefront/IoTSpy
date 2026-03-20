using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using IoTSpy.Proxy;
using IoTSpy.Proxy.Interception;
using IoTSpy.Proxy.Tls;
using IoTSpy.Storage;
using IoTSpy.Storage.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace IoTSpy.Api.IntegrationTests;

/// <summary>
/// WebApplicationFactory that replaces heavy infrastructure (TCP listeners, SharpPcap,
/// ARP spoof) with lightweight fakes so integration tests can exercise the HTTP layer
/// without requiring real network interfaces or root permissions.
/// </summary>
public class IoTSpyWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // ── Replace EF Core with in-process SQLite ────────────────────────
            services.RemoveAll<DbContextOptions<IoTSpyDbContext>>();
            services.RemoveAll<IoTSpyDbContext>();

            var dbName = $"integration-{Guid.NewGuid():N}";
            services.AddDbContext<IoTSpyDbContext>(opts =>
                opts.UseSqlite($"Data Source=file:{dbName}?mode=memory&cache=shared"));

            // ── Remove proxy infrastructure and hosted services ───────────────
            services.RemoveAll<ExplicitProxyServer>();
            services.RemoveAll<TransparentProxyServer>();
            services.RemoveAll<IptablesHelper>();
            services.RemoveAll<ArpSpoofEngine>();
            services.RemoveAll<ICertificateAuthority>();

            // Remove the ProxyService singleton (registered as IProxyService)
            services.RemoveAll<IProxyService>();
            services.RemoveAll<ProxyService>();

            // Remove the IHostedService that wraps ProxyService
            // (registered via AddHostedService(sp => (ProxyService)sp.GetRequiredService<IProxyService>()))
            var hostedServiceDescriptors = services
                .Where(d => d.ServiceType == typeof(IHostedService))
                .ToList();
            foreach (var d in hostedServiceDescriptors)
                services.Remove(d);

            // Register a no-op proxy service substitute
            var proxyService = Substitute.For<IProxyService>();
            proxyService.IsRunning.Returns(false);
            proxyService.Port.Returns(8888);
            proxyService.GetSettings().Returns(new ProxySettings());
            proxyService.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            proxyService.StopAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            services.AddSingleton(proxyService);

            // No-op certificate authority
            var ca = Substitute.For<ICertificateAuthority>();
            services.AddSingleton(ca);
        });

        // Provide the required JWT secret and SQLite as the database provider
        builder.UseSetting("Auth:JwtSecret", "integration-test-secret-that-is-long-enough");
        builder.UseSetting("Database:Provider", "sqlite");
    }

    /// <summary>
    /// Ensures the schema exists for the test database.
    /// Call this from IAsyncLifetime.InitializeAsync before making requests.
    /// </summary>
    public async Task InitializeDbAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IoTSpyDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
}
