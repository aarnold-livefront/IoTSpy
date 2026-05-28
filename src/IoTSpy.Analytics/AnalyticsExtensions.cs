using IoTSpy.Analytics.Jobs;
using IoTSpy.Analytics.Rules;
using IoTSpy.Analytics.Services;
using Microsoft.Extensions.DependencyInjection;

namespace IoTSpy.Analytics;

public static class AnalyticsExtensions
{
    public static IServiceCollection AddIoTSpyAnalytics(
        this IServiceCollection services,
        AnalyticsOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<RuleBasedTagger>();
        services.AddScoped<IInsightService, InsightService>();

        if (options.Enabled)
        {
            services.AddSingleton<InsightBatchJob>();
            services.AddHostedService(sp => sp.GetRequiredService<InsightBatchJob>());
        }

        return services;
    }
}
