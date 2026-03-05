using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using IoTSpy.Manipulation.AiMock;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Manipulation;

public static class ManipulationExtensions
{
    public static IServiceCollection AddIoTSpyManipulation(this IServiceCollection services,
        AiProviderConfig? aiConfig = null)
    {
        services.AddSingleton<RulesEngine>();
        services.AddSingleton<CSharpScriptEngine>();
        services.AddSingleton<JavaScriptEngine>();
        services.AddSingleton<ReplayService>();
        services.AddSingleton<FuzzerService>();
        services.AddSingleton<IManipulationService, ManipulationService>();

        services.AddHttpClient("IoTSpyReplay", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("IoTSpy/1.0 Replay");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            // Research tool — skip TLS validation for replaying to IoT devices
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        });

        services.AddHttpClient("IoTSpyFuzzer", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("IoTSpy/1.0 Fuzzer");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        });

        // ── AI Mock Engine ──────────────────────────────────────────────────
        if (aiConfig is not null && IsAiConfigValid(aiConfig))
        {
            services.AddSingleton(aiConfig);

            services.AddHttpClient("IoTSpyAiMock", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(120);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("IoTSpy/1.0 AiMock");
            });

            services.AddSingleton<IAiProvider>(sp =>
            {
                var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpFactory.CreateClient("IoTSpyAiMock");
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                return AiProviderFactory.Create(aiConfig, httpClient, loggerFactory);
            });

            services.AddSingleton<IAiMockService, AiMockService>();
        }

        return services;
    }

    private static bool IsAiConfigValid(AiProviderConfig config) =>
        !string.IsNullOrWhiteSpace(config.ApiKey) ||
        config.Provider.Equals("ollama", StringComparison.OrdinalIgnoreCase) ||
        config.Provider.Equals("local", StringComparison.OrdinalIgnoreCase);
}
