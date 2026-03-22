using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using IoTSpy.Scanner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text;
using Xunit;

namespace IoTSpy.Scanner.Tests;

public class ReportServiceTests
{
    private static IServiceScopeFactory BuildScopeFactory(
        Device? device,
        List<ScanJob> jobs,
        List<ScanFinding> findings)
    {
        var deviceRepo = new Mock<IDeviceRepository>();
        deviceRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(device);

        var scanJobRepo = new Mock<IScanJobRepository>();
        scanJobRepo.Setup(r => r.GetByDeviceIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(jobs);
        scanJobRepo.Setup(r => r.GetFindingsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(findings);

        var services = new ServiceCollection();
        services.AddSingleton(deviceRepo.Object);
        services.AddSingleton(scanJobRepo.Object);
        var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IServiceScopeFactory>();
    }

    private static Device MakeDevice() => new()
    {
        Id = Guid.NewGuid(),
        Label = "TestDevice",
        IpAddress = "192.168.1.1",
        MacAddress = "00:11:22:33:44:55",
        Hostname = "testdevice.local"
    };

    private static ScanFinding MakeFinding(ScanFindingSeverity severity, string title) => new()
    {
        Id = Guid.NewGuid(),
        ScanJobId = Guid.NewGuid(),
        Title = title,
        Description = $"Description for {title}",
        Severity = severity,
        Type = ScanFindingType.OpenPort
    };

    [Fact]
    public async Task GenerateHtmlReport_ReturnsHtmlWithFindings()
    {
        var device = MakeDevice();
        var job = new ScanJob { Id = Guid.NewGuid(), DeviceId = device.Id, TargetIp = device.IpAddress };
        var findings = new List<ScanFinding>
        {
            MakeFinding(ScanFindingSeverity.Critical, "Critical Issue"),
            MakeFinding(ScanFindingSeverity.High, "High Issue"),
            MakeFinding(ScanFindingSeverity.Medium, "Medium Issue")
        };

        var scopeFactory = BuildScopeFactory(device, [job], findings);
        var service = new ReportService(scopeFactory, NullLogger<ReportService>.Instance);

        var bytes = await service.GenerateHtmlReportAsync(device.Id, TestContext.Current.CancellationToken);
        var html = Encoding.UTF8.GetString(bytes);

        Assert.Contains("TestDevice", html);
        Assert.Contains("Critical Issue", html);
        Assert.Contains("High Issue", html);
        Assert.Contains("Medium Issue", html);
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("Critical", html);
    }

    [Fact]
    public async Task GenerateHtmlReport_EmptyFindings_ReturnsEmptyTable()
    {
        var device = MakeDevice();
        var job = new ScanJob { Id = Guid.NewGuid(), DeviceId = device.Id, TargetIp = device.IpAddress };

        var scopeFactory = BuildScopeFactory(device, [job], []);
        var service = new ReportService(scopeFactory, NullLogger<ReportService>.Instance);

        var bytes = await service.GenerateHtmlReportAsync(device.Id, TestContext.Current.CancellationToken);
        var html = Encoding.UTF8.GetString(bytes);

        Assert.Contains("No findings", html);
        Assert.DoesNotContain("<tr><td>OpenPort</td>", html);
    }

    [Fact]
    public async Task GeneratePdfReport_ReturnsBytesStartingWithPdfMagic()
    {
        var device = MakeDevice();
        var job = new ScanJob { Id = Guid.NewGuid(), DeviceId = device.Id, TargetIp = device.IpAddress };
        var findings = new List<ScanFinding>
        {
            MakeFinding(ScanFindingSeverity.High, "High Finding")
        };

        var scopeFactory = BuildScopeFactory(device, [job], findings);
        var service = new ReportService(scopeFactory, NullLogger<ReportService>.Instance);

        var bytes = await service.GeneratePdfReportAsync(device.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        // PDF magic bytes: %PDF
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
    }
}
