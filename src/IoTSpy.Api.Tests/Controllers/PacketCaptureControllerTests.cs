using IoTSpy.Api.Controllers;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;
using ControllerFilterDto = IoTSpy.Api.Controllers.PacketFilterDto;
using ServiceFilterDto = IoTSpy.Core.Interfaces.PacketFilterDto;

namespace IoTSpy.Api.Tests.Controllers;

public class PacketCaptureControllerTests
{
    private static PacketCaptureController MakeController(
        IPacketCaptureService? captureService = null,
        ICaptureDeviceRepository? deviceRepo = null,
        IPacketCaptureAnalyzer? analyzer = null)
    {
        return new PacketCaptureController(
            captureService ?? Substitute.For<IPacketCaptureService>(),
            deviceRepo ?? Substitute.For<ICaptureDeviceRepository>(),
            analyzer ?? Substitute.For<IPacketCaptureAnalyzer>());
    }

    private static CaptureDevice MakeDevice(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = "eth0",
        DisplayName = "Ethernet",
        IpAddress = "192.168.1.1",
        MacAddress = "AA:BB:CC:DD:EE:FF"
    };

    private static CapturedPacket MakePacket(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Timestamp = DateTimeOffset.UtcNow,
        Protocol = "TCP",
        SourceIp = "192.168.1.2",
        DestinationIp = "192.168.1.1",
        SourcePort = 12345,
        DestinationPort = 80,
        Length = 100,
        PayloadPreview = "GET / HTTP/1.1"
    };

    private static FreezeFrameResult MakeFreezeFrame(Guid packetId) => new()
    {
        PacketId = packetId,
        Timestamp = DateTimeOffset.UtcNow,
        FullPayloadHex = "4745542f",
        HexDump = "47 45 54 2f",
        ProtocolDetails = "HTTP GET",
        Layer2Info = "Ethernet II",
        Layer3Info = "IPv4",
        Layer4Info = "TCP"
    };

    [Fact]
    public async Task ListDevices_SyncsInterfacesAndReturnsDeviceDtos()
    {
        var device = MakeDevice();
        var captureService = Substitute.For<IPacketCaptureService>();
        captureService.ListInterfacesAsync().Returns(new List<NetworkDevice>());
        var deviceRepo = Substitute.For<ICaptureDeviceRepository>();
        deviceRepo.GetAllAsync().Returns(new List<CaptureDevice> { device });

        var controller = MakeController(captureService, deviceRepo);
        var result = await controller.ListDevices() as OkObjectResult;

        Assert.NotNull(result);
        await captureService.Received(1).ListInterfacesAsync();
        var dtos = Assert.IsAssignableFrom<IEnumerable<CaptureDeviceDto>>(result.Value);
        Assert.Single(dtos);
    }

    [Fact]
    public async Task GetDevice_WhenFound_ReturnsOkWithDto()
    {
        var device = MakeDevice();
        var deviceRepo = Substitute.For<ICaptureDeviceRepository>();
        deviceRepo.GetByIdAsync(device.Id).Returns(device);

        var controller = MakeController(deviceRepo: deviceRepo);
        var result = await controller.GetDevice(device.Id) as OkObjectResult;

        Assert.NotNull(result);
        var dto = Assert.IsType<CaptureDeviceDto>(result.Value);
        Assert.Equal(device.Id, dto.Id);
        Assert.Equal("eth0", dto.Name);
    }

    [Fact]
    public async Task GetDevice_WhenNotFound_ReturnsNotFound()
    {
        var deviceRepo = Substitute.For<ICaptureDeviceRepository>();
        deviceRepo.GetByIdAsync(Arg.Any<Guid>()).Returns((CaptureDevice?)null);

        var controller = MakeController(deviceRepo: deviceRepo);
        var result = await controller.GetDevice(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task StartCapture_CallsServiceAndReturnsOk()
    {
        var deviceId = Guid.NewGuid();
        var captureService = Substitute.For<IPacketCaptureService>();
        captureService.StartCaptureAsync(deviceId, Arg.Any<CancellationToken>()).Returns(true);

        var controller = MakeController(captureService);
        var result = await controller.StartCapture(new StartCaptureRequest { DeviceId = deviceId }) as OkObjectResult;

        Assert.NotNull(result);
        await captureService.Received(1).StartCaptureAsync(deviceId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StopCapture_CallsServiceAndReturnsOk()
    {
        var captureService = Substitute.For<IPacketCaptureService>();
        captureService.StopCaptureAsync().Returns(true);

        var controller = MakeController(captureService);
        var result = await controller.StopCapture() as OkObjectResult;

        Assert.NotNull(result);
        await captureService.Received(1).StopCaptureAsync();
    }

    [Fact]
    public void GetStatus_ReflectsServiceIsCaptureActive()
    {
        var captureService = Substitute.For<IPacketCaptureService>();
        captureService.IsCaptureActive.Returns(true);

        var controller = MakeController(captureService);
        var result = controller.GetStatus() as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("\"isCapturing\":true", json);
    }

    [Fact]
    public void GetStatus_ReturnsFalseWhenCaptureInactive()
    {
        var captureService = Substitute.For<IPacketCaptureService>();
        captureService.IsCaptureActive.Returns(false);

        var controller = MakeController(captureService);
        var result = controller.GetStatus() as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("\"isCapturing\":false", json);
    }

    [Fact]
    public async Task GetPackets_WithFilter_ReturnsFilteredPacketDtos()
    {
        var packet = MakePacket();
        var captureService = Substitute.For<IPacketCaptureService>();
        captureService.FilterPacketsAsync(Arg.Any<ServiceFilterDto>(), Arg.Any<CancellationToken>())
            .Returns(new List<CapturedPacket> { packet });

        var controller = MakeController(captureService);
        var filter = new ControllerFilterDto { Protocol = "TCP", Limit = 10 };
        var result = await controller.GetPackets(filter) as OkObjectResult;

        Assert.NotNull(result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<CapturedPacketDto>>(result.Value);
        var list = dtos.ToList();
        Assert.Single(list);
        Assert.Equal("TCP", list[0].Protocol);
    }

    [Fact]
    public async Task GetPackets_MapsAllFilterFields()
    {
        var captureService = Substitute.For<IPacketCaptureService>();
        captureService.FilterPacketsAsync(Arg.Any<ServiceFilterDto>(), Arg.Any<CancellationToken>())
            .Returns(new List<CapturedPacket>());

        var controller = MakeController(captureService);
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var filter = new ControllerFilterDto
        {
            Protocol = "UDP",
            SourceIp = "10.0.0.1",
            DestinationIp = "10.0.0.2",
            SourcePort = 5000,
            DestinationPort = 53,
            MacAddress = "AA:BB:CC:DD:EE:FF",
            ShowOnlyErrors = true,
            ShowOnlyRetransmissions = true,
            FromTime = from,
            PayloadSearch = "DNS",
            Limit = 500
        };
        await controller.GetPackets(filter);

        await captureService.Received(1).FilterPacketsAsync(
            Arg.Is<ServiceFilterDto>(f =>
                f.Protocol == "UDP" &&
                f.SourceIp == "10.0.0.1" &&
                f.DestinationIp == "10.0.0.2" &&
                f.SourcePort == 5000 &&
                f.DestinationPort == 53 &&
                f.ShowOnlyErrors == true &&
                f.ShowOnlyRetransmissions == true &&
                f.PayloadSearch == "DNS" &&
                f.Limit == 500),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPacket_WhenFound_ReturnsOkWithDto()
    {
        var packet = MakePacket();
        var captureService = Substitute.For<IPacketCaptureService>();
        captureService.GetPacketByIdAsync(packet.Id, Arg.Any<CancellationToken>()).Returns(packet);

        var controller = MakeController(captureService);
        var result = await controller.GetPacket(packet.Id) as OkObjectResult;

        Assert.NotNull(result);
        var dto = Assert.IsType<CapturedPacketDto>(result.Value);
        Assert.Equal(packet.Id, dto.Id);
        Assert.Equal("TCP", dto.Protocol);
    }

    [Fact]
    public async Task GetPacket_WhenNotFound_ReturnsNotFound()
    {
        var captureService = Substitute.For<IPacketCaptureService>();
        captureService.GetPacketByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((CapturedPacket?)null);

        var controller = MakeController(captureService);
        var result = await controller.GetPacket(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task FreezePacket_WhenFound_ReturnsFreezeFrameDto()
    {
        var id = Guid.NewGuid();
        var frame = MakeFreezeFrame(id);
        var captureService = Substitute.For<IPacketCaptureService>();
        captureService.FreezeFrameAsync(id, Arg.Any<CancellationToken>()).Returns(frame);

        var controller = MakeController(captureService);
        var result = await controller.FreezePacket(id) as OkObjectResult;

        Assert.NotNull(result);
        var dto = Assert.IsType<FreezeFrameDto>(result.Value);
        Assert.Equal(id, dto.PacketId);
        Assert.Equal("HTTP GET", dto.ProtocolDetails);
    }

    [Fact]
    public async Task FreezePacket_WhenNotFound_ReturnsNotFound()
    {
        var captureService = Substitute.For<IPacketCaptureService>();
        captureService.FreezeFrameAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((FreezeFrameResult?)null);

        var controller = MakeController(captureService);
        var result = await controller.FreezePacket(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetFreezeFrame_WhenFound_ReturnsFreezeFrameDto()
    {
        var id = Guid.NewGuid();
        var frame = MakeFreezeFrame(id);
        var captureService = Substitute.For<IPacketCaptureService>();
        captureService.GetFreezeFrameAsync(id, Arg.Any<CancellationToken>()).Returns(frame);

        var controller = MakeController(captureService);
        var result = await controller.GetFreezeFrame(id) as OkObjectResult;

        Assert.NotNull(result);
        var dto = Assert.IsType<FreezeFrameDto>(result.Value);
        Assert.Equal(id, dto.PacketId);
    }

    [Fact]
    public async Task GetFreezeFrame_WhenNotFound_ReturnsNotFound()
    {
        var captureService = Substitute.For<IPacketCaptureService>();
        captureService.GetFreezeFrameAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((FreezeFrameResult?)null);

        var controller = MakeController(captureService);
        var result = await controller.GetFreezeFrame(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeletePacket_ReturnsOkWithPacketId()
    {
        var id = Guid.NewGuid();
        var captureService = Substitute.For<IPacketCaptureService>();
        captureService.DeletePacketAsync(id, Arg.Any<CancellationToken>()).Returns(true);

        var controller = MakeController(captureService);
        var result = await controller.DeletePacket(id) as OkObjectResult;

        Assert.NotNull(result);
        await captureService.Received(1).DeletePacketAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetProtocolDistribution_WhenDataAvailable_ReturnsDto()
    {
        var dist = new ProtocolDistribution
        {
            TotalPackets = 50,
            ByProtocol = new() { new ProtocolStats { Name = "TCP", Count = 40, Percentage = 80.0 } },
            ByLayer3 = new(),
            ByLayer4 = new()
        };
        var analyzer = Substitute.For<IPacketCaptureAnalyzer>();
        analyzer.AnalyzeProtocolsAsync(Arg.Any<CancellationToken>()).Returns(dist);

        var controller = MakeController(analyzer: analyzer);
        var result = await controller.GetProtocolDistribution() as OkObjectResult;

        Assert.NotNull(result);
        var dto = Assert.IsType<ProtocolDistributionDto>(result.Value);
        Assert.Equal(50, dto.TotalPackets);
        Assert.Single(dto.ByProtocol);
        Assert.Equal("TCP", dto.ByProtocol[0].Name);
    }

    [Fact]
    public async Task GetProtocolDistribution_WhenNoData_ReturnsNotFound()
    {
        var analyzer = Substitute.For<IPacketCaptureAnalyzer>();
        analyzer.AnalyzeProtocolsAsync(Arg.Any<CancellationToken>()).Returns((ProtocolDistribution?)null);

        var controller = MakeController(analyzer: analyzer);
        var result = await controller.GetProtocolDistribution();

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetCommunicationPatterns_ReturnsMappedDtos()
    {
        var patterns = new List<CommunicationPattern>
        {
            new() { SourceIp = "10.0.0.1", DestinationIp = "10.0.0.2", PacketCount = 15, TotalBytes = 1500, ProtocolsUsed = new() { "TCP" } }
        };
        var analyzer = Substitute.For<IPacketCaptureAnalyzer>();
        analyzer.FindCommunicationPatternsAsync(5, Arg.Any<CancellationToken>()).Returns(patterns);

        var controller = MakeController(analyzer: analyzer);
        var result = await controller.GetCommunicationPatterns(topN: 5) as OkObjectResult;

        Assert.NotNull(result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<CommunicationPatternDto>>(result.Value);
        var list = dtos.ToList();
        Assert.Single(list);
        Assert.Equal("10.0.0.1", list[0].SourceIp);
        Assert.Equal(15, list[0].PacketCount);
    }

    [Fact]
    public async Task GetSuspiciousActivity_ReturnsMappedDtos()
    {
        var activities = new List<SuspiciousActivity>
        {
            new() { Category = "PortScan", Severity = "High", Description = "Port scan detected", SourceIp = "192.168.1.50", PacketCount = 200, Evidence = new() { "SYN to 200 ports" } }
        };
        var analyzer = Substitute.For<IPacketCaptureAnalyzer>();
        analyzer.DetectSuspiciousActivityAsync(Arg.Any<CancellationToken>()).Returns(activities);

        var controller = MakeController(analyzer: analyzer);
        var result = await controller.GetSuspiciousActivity() as OkObjectResult;

        Assert.NotNull(result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<SuspiciousActivityDto>>(result.Value);
        var list = dtos.ToList();
        Assert.Single(list);
        Assert.Equal("PortScan", list[0].Category);
        Assert.Equal("High", list[0].Severity);
    }

    [Fact]
    public async Task ImportPcap_WhenNoFile_ReturnsBadRequest()
    {
        var controller = MakeController();
        var result = await controller.ImportPcap(null!, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ImportPcap_WhenInvalidExtension_ReturnsBadRequest()
    {
        var file = Substitute.For<IFormFile>();
        file.FileName.Returns("capture.txt");
        file.Length.Returns(100L);

        var controller = MakeController();
        var result = await controller.ImportPcap(file, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ImportPcap_WhenValidPcapFile_ReturnsImportStats()
    {
        var importResult = new PcapImportResult
        {
            Success = true,
            JobId = "job-1",
            PacketsImported = 100,
            PacketsSkipped = 2,
            TcpSessionsReconstructed = 5
        };
        var captureService = Substitute.For<IPacketCaptureService>();
        captureService.ImportFromPcapAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(importResult);

        var file = Substitute.For<IFormFile>();
        file.FileName.Returns("capture.pcap");
        file.Length.Returns(1024L);
        file.OpenReadStream().Returns(new MemoryStream(new byte[1024]));

        var controller = MakeController(captureService);
        var result = await controller.ImportPcap(file, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("job-1", json);
        Assert.Contains("100", json);
    }

    [Fact]
    public async Task ImportPcap_WhenImportFails_ReturnsBadRequest()
    {
        var importResult = new PcapImportResult { Success = false, Error = "Corrupt file", JobId = "job-2" };
        var captureService = Substitute.For<IPacketCaptureService>();
        captureService.ImportFromPcapAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(importResult);

        var file = Substitute.For<IFormFile>();
        file.FileName.Returns("capture.pcapng");
        file.Length.Returns(1024L);
        file.OpenReadStream().Returns(new MemoryStream(new byte[1024]));

        var controller = MakeController(captureService);
        var result = await controller.ImportPcap(file, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ExportPcap_WhenNoFilters_CallsUnfilteredExport()
    {
        var pcapBytes = new byte[] { 0xd4, 0xc3, 0xb2, 0xa1 };
        var captureService = Substitute.For<IPacketCaptureService>();
        captureService.ExportToPcapAsync(Arg.Any<CancellationToken>()).Returns(pcapBytes);

        var controller = MakeController(captureService);
        var result = await controller.ExportPcap(null, null, null, null, null) as FileContentResult;

        Assert.NotNull(result);
        Assert.Equal("application/vnd.tcpdump.pcap", result.ContentType);
        Assert.Equal("capture.pcap", result.FileDownloadName);
        await captureService.Received(1).ExportToPcapAsync(Arg.Any<CancellationToken>());
        await captureService.DidNotReceive().ExportToPcapFilteredAsync(Arg.Any<ServiceFilterDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportPcap_WhenFilterProvided_CallsFilteredExport()
    {
        var pcapBytes = new byte[] { 0xd4, 0xc3, 0xb2, 0xa1 };
        var captureService = Substitute.For<IPacketCaptureService>();
        captureService.ExportToPcapFilteredAsync(Arg.Any<ServiceFilterDto>(), Arg.Any<CancellationToken>()).Returns(pcapBytes);

        var controller = MakeController(captureService);
        var result = await controller.ExportPcap("TCP", null, null, null, null) as FileContentResult;

        Assert.NotNull(result);
        await captureService.Received(1).ExportToPcapFilteredAsync(Arg.Any<ServiceFilterDto>(), Arg.Any<CancellationToken>());
        await captureService.DidNotReceive().ExportToPcapAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportPcap_WhenNoPcapData_ReturnsNotFound()
    {
        var captureService = Substitute.For<IPacketCaptureService>();
        captureService.ExportToPcapAsync(Arg.Any<CancellationToken>()).Returns((byte[]?)null);

        var controller = MakeController(captureService);
        var result = await controller.ExportPcap(null, null, null, null, null);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void FreezeFrame_CallsAnalyzerAndReturnsOk()
    {
        var analyzer = Substitute.For<IPacketCaptureAnalyzer>();
        var controller = MakeController(analyzer: analyzer);

        var result = controller.FreezeFrame() as OkObjectResult;

        Assert.NotNull(result);
        analyzer.Received(1).FreezeFrame();
    }

    [Fact]
    public void UnfreezeFrame_CallsAnalyzerAndReturnsOk()
    {
        var analyzer = Substitute.For<IPacketCaptureAnalyzer>();
        var controller = MakeController(analyzer: analyzer);

        var result = controller.UnfreezeFrame() as OkObjectResult;

        Assert.NotNull(result);
        analyzer.Received(1).UnfreezeFrame();
    }

    [Fact]
    public void GetFreezeStatus_ReturnsIsFrozenAndCount()
    {
        var analyzer = Substitute.For<IPacketCaptureAnalyzer>();
        analyzer.IsFrozen.Returns(true);
        analyzer.FilteredPacketCount.Returns(42);

        var controller = MakeController(analyzer: analyzer);
        var result = controller.GetFreezeStatus() as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("isFrozen", json);
        Assert.Contains("42", json);
    }
}
