using IoTSpy.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace IoTSpy.Scanner;

public static class ScannerExtensions
{
    public static IServiceCollection AddIoTSpyScanner(this IServiceCollection services)
    {
        services.AddSingleton<PortScanner>();
        services.AddSingleton<ServiceFingerprinter>();
        services.AddSingleton<CredentialTester>();
        services.AddSingleton<ConfigAuditor>();
        services.AddSingleton<IScannerService, ScannerService>();
        services.AddSingleton<IPacketCaptureService, PacketCaptureService>();
        services.AddSingleton<IReportService, ReportService>();

        services.AddHttpClient<CveLookupService>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("IoTSpy/1.0");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}
