using IoTSpy.Api.Controllers;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace IoTSpy.Api.Tests.Controllers;

// Shared factory to keep tests concise after adding IScanScopeRepository as 4th ctor arg.
file static class ScannerControllerFactory
{
    public static ScannerController Make(
        IScannerService? scanner = null,
        IScanJobRepository? scanJobs = null,
        IDeviceRepository? devices = null,
        IScanScopeRepository? scopes = null)
    {
        if (scopes is null)
        {
            scopes = Substitute.For<IScanScopeRepository>();
            scopes.GetActiveAsync(Arg.Any<CancellationToken>()).Returns(new List<ScanScope>());
        }
        return new ScannerController(
            scanner  ?? Substitute.For<IScannerService>(),
            scanJobs ?? Substitute.For<IScanJobRepository>(),
            devices  ?? Substitute.For<IDeviceRepository>(),
            scopes);
    }
}

public class ScannerControllerTests
{
    private static ScanJob MakeScanJob(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        TargetIp = "192.168.1.100",
        Status = ScanStatus.Pending
    };

    private static Device MakeDevice(string ip = "192.168.1.100") =>
        new() { Id = Guid.NewGuid(), IpAddress = ip };

    // ── StartScan happy path ──────────────────────────────────────────────────

    [Fact]
    public async Task StartScan_WhenDeviceExistsAndConsentGiven_ReturnsScanJob()
    {
        var device = MakeDevice();
        var job = MakeScanJob();

        var scanner = Substitute.For<IScannerService>();
        scanner.StartScanAsync(Arg.Any<ScanJob>(), Arg.Any<CancellationToken>()).Returns(job);
        var devices = Substitute.For<IDeviceRepository>();
        devices.GetByIdAsync(device.Id, Arg.Any<CancellationToken>()).Returns(device);

        var controller = ScannerControllerFactory.Make(scanner: scanner, devices: devices);
        var dto = new StartScanDto(device.Id, ConsentAcknowledged: true);
        var result = await controller.StartScan(dto) as OkObjectResult;

        Assert.NotNull(result);
        await scanner.Received(1).StartScanAsync(Arg.Any<ScanJob>(), Arg.Any<CancellationToken>());
    }

    // ── Consent gate ─────────────────────────────────────────────────────────

    [Fact]
    public async Task StartScan_WithoutConsent_ReturnsBadRequest()
    {
        var device = MakeDevice();
        var devices = Substitute.For<IDeviceRepository>();
        devices.GetByIdAsync(device.Id, Arg.Any<CancellationToken>()).Returns(device);

        var scanner = Substitute.For<IScannerService>();
        var controller = ScannerControllerFactory.Make(scanner: scanner, devices: devices);
        var dto = new StartScanDto(device.Id, ConsentAcknowledged: false);

        var result = await controller.StartScan(dto);

        Assert.IsType<BadRequestObjectResult>(result);
        await scanner.DidNotReceive().StartScanAsync(Arg.Any<ScanJob>(), Arg.Any<CancellationToken>());
    }

    // ── Scope gate ───────────────────────────────────────────────────────────

    [Fact]
    public async Task StartScan_WhenScopesExistAndIpInScope_Allows()
    {
        var device = MakeDevice("192.168.1.50");
        var devices = Substitute.For<IDeviceRepository>();
        devices.GetByIdAsync(device.Id, Arg.Any<CancellationToken>()).Returns(device);

        var scanner = Substitute.For<IScannerService>();
        scanner.StartScanAsync(Arg.Any<ScanJob>(), Arg.Any<CancellationToken>()).Returns(MakeScanJob());

        var scopes = Substitute.For<IScanScopeRepository>();
        scopes.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ScanScope> { new() { Cidr = "192.168.1.0/24", IsActive = true } });

        var controller = ScannerControllerFactory.Make(scanner: scanner, devices: devices, scopes: scopes);
        var dto = new StartScanDto(device.Id, ConsentAcknowledged: true);

        var result = await controller.StartScan(dto);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task StartScan_WhenScopesExistAndIpOutOfScope_ReturnsForbidden()
    {
        var device = MakeDevice("10.99.0.1");
        var devices = Substitute.For<IDeviceRepository>();
        devices.GetByIdAsync(device.Id, Arg.Any<CancellationToken>()).Returns(device);

        var scanner = Substitute.For<IScannerService>();
        var scopes = Substitute.For<IScanScopeRepository>();
        scopes.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ScanScope> { new() { Cidr = "192.168.1.0/24", IsActive = true } });

        var controller = ScannerControllerFactory.Make(scanner: scanner, devices: devices, scopes: scopes);
        var dto = new StartScanDto(device.Id, ConsentAcknowledged: true);

        var result = await controller.StartScan(dto);

        Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, ((ObjectResult)result).StatusCode);
        await scanner.DidNotReceive().StartScanAsync(Arg.Any<ScanJob>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartScan_WhenNoScopesConfigured_AllowsAnyIp()
    {
        var device = MakeDevice("1.2.3.4");
        var devices = Substitute.For<IDeviceRepository>();
        devices.GetByIdAsync(device.Id, Arg.Any<CancellationToken>()).Returns(device);

        var scanner = Substitute.For<IScannerService>();
        scanner.StartScanAsync(Arg.Any<ScanJob>(), Arg.Any<CancellationToken>()).Returns(MakeScanJob());

        // ScannerControllerFactory default: empty scope list → gate open
        var controller = ScannerControllerFactory.Make(scanner: scanner, devices: devices);
        var dto = new StartScanDto(device.Id, ConsentAcknowledged: true);

        var result = await controller.StartScan(dto);

        Assert.IsType<OkObjectResult>(result);
    }

    // ── Port range validation ─────────────────────────────────────────────────

    [Fact]
    public async Task StartScan_RejectsOverlongPortRangeString()
    {
        var device = MakeDevice();
        var devices = Substitute.For<IDeviceRepository>();
        devices.GetByIdAsync(device.Id, Arg.Any<CancellationToken>()).Returns(device);

        var scanner = Substitute.For<IScannerService>();
        var controller = ScannerControllerFactory.Make(scanner: scanner, devices: devices);
        var hugeRange = string.Join(",", Enumerable.Repeat("1-65535", 50));
        var dto = new StartScanDto(device.Id, PortRange: hugeRange, ConsentAcknowledged: true);

        var result = await controller.StartScan(dto);

        Assert.IsType<BadRequestObjectResult>(result);
        await scanner.DidNotReceive().StartScanAsync(Arg.Any<ScanJob>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartScan_ClampsExcessiveMaxConcurrency()
    {
        var device = MakeDevice();
        var devices = Substitute.For<IDeviceRepository>();
        devices.GetByIdAsync(device.Id, Arg.Any<CancellationToken>()).Returns(device);

        var scanner = Substitute.For<IScannerService>();
        scanner.StartScanAsync(Arg.Any<ScanJob>(), Arg.Any<CancellationToken>()).Returns(MakeScanJob());

        var controller = ScannerControllerFactory.Make(scanner: scanner, devices: devices);
        var dto = new StartScanDto(device.Id, PortRange: "1-100", MaxConcurrency: 10_000, ConsentAcknowledged: true);

        await controller.StartScan(dto);

        await scanner.Received(1).StartScanAsync(
            Arg.Is<ScanJob>(j => j.MaxConcurrency == 100),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartScan_DefaultMaxConcurrencyIs25NotAggressive100()
    {
        var device = MakeDevice();
        var devices = Substitute.For<IDeviceRepository>();
        devices.GetByIdAsync(device.Id, Arg.Any<CancellationToken>()).Returns(device);

        var scanner = Substitute.For<IScannerService>();
        scanner.StartScanAsync(Arg.Any<ScanJob>(), Arg.Any<CancellationToken>()).Returns(MakeScanJob());

        var controller = ScannerControllerFactory.Make(scanner: scanner, devices: devices);
        var dto = new StartScanDto(device.Id, ConsentAcknowledged: true);

        await controller.StartScan(dto);

        await scanner.Received(1).StartScanAsync(
            Arg.Is<ScanJob>(j => j.MaxConcurrency == 25),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartScan_WhenDeviceNotFound_ReturnsNotFound()
    {
        var devices = Substitute.For<IDeviceRepository>();
        devices.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Device?)null);

        var controller = ScannerControllerFactory.Make(devices: devices);
        var result = await controller.StartScan(new StartScanDto(Guid.NewGuid(), ConsentAcknowledged: true));

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ── Job management ────────────────────────────────────────────────────────

    [Fact]
    public async Task ListJobs_ReturnsAllJobs()
    {
        var scanJobs = Substitute.For<IScanJobRepository>();
        scanJobs.GetAllAsync(1, 20, Arg.Any<ScanStatus?>(), Arg.Any<Guid?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
                .Returns(new List<ScanJob> { MakeScanJob(), MakeScanJob() });
        scanJobs.CountAsync(Arg.Any<ScanStatus?>(), Arg.Any<Guid?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
                .Returns(2);

        var controller = ScannerControllerFactory.Make(scanJobs: scanJobs);
        var result = await controller.ListJobs(1, 20, ct: TestContext.Current.CancellationToken) as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("\"total\":2", json);
        Assert.Contains("\"items\"", json);
    }

    [Fact]
    public async Task GetJob_WhenFound_ReturnsJob()
    {
        var id = Guid.NewGuid();
        var scanJobs = Substitute.For<IScanJobRepository>();
        scanJobs.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(MakeScanJob(id));

        var result = await ScannerControllerFactory.Make(scanJobs: scanJobs).GetJob(id) as OkObjectResult;

        Assert.NotNull(result);
        Assert.IsType<ScanJob>(result.Value);
    }

    [Fact]
    public async Task GetJob_WhenNotFound_ReturnsNotFound()
    {
        var scanJobs = Substitute.For<IScanJobRepository>();
        scanJobs.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ScanJob?)null);

        var result = await ScannerControllerFactory.Make(scanJobs: scanJobs).GetJob(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CancelScan_WhenRunning_ReturnsOk()
    {
        var id = Guid.NewGuid();
        var scanJobs = Substitute.For<IScanJobRepository>();
        scanJobs.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(MakeScanJob(id));

        var scanner = Substitute.For<IScannerService>();
        scanner.IsScanRunning(id).Returns(true);

        var controller = ScannerControllerFactory.Make(scanner: scanner, scanJobs: scanJobs);
        var result = await controller.CancelScan(id) as OkResult;

        Assert.NotNull(result);
        await scanner.Received(1).CancelScanAsync(id);
    }

    [Fact]
    public async Task DeleteJob_CallsDeleteAndReturnsNoContent()
    {
        var id = Guid.NewGuid();
        var scanJobs = Substitute.For<IScanJobRepository>();
        scanJobs.DeleteAsync(id, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var controller = ScannerControllerFactory.Make(scanJobs: scanJobs);
        var result = await controller.DeleteJob(id);

        Assert.IsType<NoContentResult>(result);
        await scanJobs.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }
}
