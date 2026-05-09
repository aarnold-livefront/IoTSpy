using IoTSpy.Api.Controllers;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace IoTSpy.Api.Tests.Controllers;

public class ScannerControllerTests
{
    private static ScanJob MakeScanJob(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        TargetIp = "192.168.1.100",
        Status = ScanStatus.Pending
    };

    [Fact]
    public async Task StartScan_WhenDeviceExists_ReturnsScanJob()
    {
        var device = new Device { Id = Guid.NewGuid(), IpAddress = "192.168.1.100" };
        var job = MakeScanJob();

        var scanner = Substitute.For<IScannerService>();
        scanner.StartScanAsync(Arg.Any<ScanJob>(), Arg.Any<CancellationToken>()).Returns(job);

        var scanJobs = Substitute.For<IScanJobRepository>();
        var devices = Substitute.For<IDeviceRepository>();
        devices.GetByIdAsync(device.Id, Arg.Any<CancellationToken>()).Returns(device);

        var controller = new ScannerController(scanner, scanJobs, devices);
        var dto = new StartScanDto(device.Id, null, null, null, null, null, null, null);
        var result = await controller.StartScan(dto) as OkObjectResult;

        Assert.NotNull(result);
        await scanner.Received(1).StartScanAsync(Arg.Any<ScanJob>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartScan_RejectsOverlongPortRangeString()
    {
        var device = new Device { Id = Guid.NewGuid(), IpAddress = "192.168.1.100" };
        var scanner = Substitute.For<IScannerService>();
        var scanJobs = Substitute.For<IScanJobRepository>();
        var devices = Substitute.For<IDeviceRepository>();
        devices.GetByIdAsync(device.Id, Arg.Any<CancellationToken>()).Returns(device);

        var controller = new ScannerController(scanner, scanJobs, devices);
        // 257 chars: "1-65535," repeated 32 times + "1" = "1-65535,1-65535,...,1" → 257
        var hugeRange = string.Join(",", Enumerable.Repeat("1-65535", 50));
        var dto = new StartScanDto(device.Id, hugeRange, null, null, null, null, null, null);

        var result = await controller.StartScan(dto);

        Assert.IsType<BadRequestObjectResult>(result);
        await scanner.DidNotReceive().StartScanAsync(Arg.Any<ScanJob>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartScan_ClampsExcessiveMaxConcurrency()
    {
        var device = new Device { Id = Guid.NewGuid(), IpAddress = "192.168.1.100" };
        var scanner = Substitute.For<IScannerService>();
        scanner.StartScanAsync(Arg.Any<ScanJob>(), Arg.Any<CancellationToken>()).Returns(MakeScanJob());
        var scanJobs = Substitute.For<IScanJobRepository>();
        var devices = Substitute.For<IDeviceRepository>();
        devices.GetByIdAsync(device.Id, Arg.Any<CancellationToken>()).Returns(device);

        var controller = new ScannerController(scanner, scanJobs, devices);
        var dto = new StartScanDto(device.Id, "1-100", 10_000, null, null, null, null, null);

        await controller.StartScan(dto);

        await scanner.Received(1).StartScanAsync(
            Arg.Is<ScanJob>(j => j.MaxConcurrency == 100),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartScan_DefaultMaxConcurrencyIs25NotAggressive100()
    {
        // Regression guard: previous default was 100, which SYN-floods small subnets.
        var device = new Device { Id = Guid.NewGuid(), IpAddress = "192.168.1.100" };
        var scanner = Substitute.For<IScannerService>();
        scanner.StartScanAsync(Arg.Any<ScanJob>(), Arg.Any<CancellationToken>()).Returns(MakeScanJob());
        var scanJobs = Substitute.For<IScanJobRepository>();
        var devices = Substitute.For<IDeviceRepository>();
        devices.GetByIdAsync(device.Id, Arg.Any<CancellationToken>()).Returns(device);

        var controller = new ScannerController(scanner, scanJobs, devices);
        var dto = new StartScanDto(device.Id, null, null, null, null, null, null, null);

        await controller.StartScan(dto);

        await scanner.Received(1).StartScanAsync(
            Arg.Is<ScanJob>(j => j.MaxConcurrency == 25),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartScan_WhenDeviceNotFound_ReturnsNotFound()
    {
        var scanner = Substitute.For<IScannerService>();
        var scanJobs = Substitute.For<IScanJobRepository>();
        var devices = Substitute.For<IDeviceRepository>();
        devices.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Device?)null);

        var controller = new ScannerController(scanner, scanJobs, devices);
        var result = await controller.StartScan(new StartScanDto(Guid.NewGuid(), null, null, null, null, null, null, null));

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ListJobs_ReturnsAllJobs()
    {
        var scanJobs = Substitute.For<IScanJobRepository>();
        scanJobs.GetAllAsync(1, 20, Arg.Any<ScanStatus?>(), Arg.Any<Guid?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>()).Returns(new List<ScanJob> { MakeScanJob(), MakeScanJob() });
        scanJobs.CountAsync(Arg.Any<ScanStatus?>(), Arg.Any<Guid?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>()).Returns(2);

        var controller = new ScannerController(
            Substitute.For<IScannerService>(), scanJobs, Substitute.For<IDeviceRepository>());

        var result = await controller.ListJobs(1, 20, ct: CancellationToken.None) as OkObjectResult;

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

        var controller = new ScannerController(
            Substitute.For<IScannerService>(), scanJobs, Substitute.For<IDeviceRepository>());

        var result = await controller.GetJob(id) as OkObjectResult;

        Assert.NotNull(result);
        Assert.IsType<ScanJob>(result.Value);
    }

    [Fact]
    public async Task GetJob_WhenNotFound_ReturnsNotFound()
    {
        var scanJobs = Substitute.For<IScanJobRepository>();
        scanJobs.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ScanJob?)null);

        var controller = new ScannerController(
            Substitute.For<IScannerService>(), scanJobs, Substitute.For<IDeviceRepository>());

        var result = await controller.GetJob(Guid.NewGuid());

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

        var controller = new ScannerController(scanner, scanJobs, Substitute.For<IDeviceRepository>());
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

        var controller = new ScannerController(
            Substitute.For<IScannerService>(), scanJobs, Substitute.For<IDeviceRepository>());

        var result = await controller.DeleteJob(id);

        Assert.IsType<NoContentResult>(result);
        await scanJobs.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }
}
