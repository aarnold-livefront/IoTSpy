using IoTSpy.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace IoTSpy.Manipulation;

public static class ManipulationExtensions
{
    public static IServiceCollection AddIoTSpyManipulation(this IServiceCollection services)
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

        return services;
    }
}
