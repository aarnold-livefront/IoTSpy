using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTSpy.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/packet-capture")]
public class PacketCaptureController : ControllerBase
{
    private readonly IPacketCaptureService _captureService;
    private readonly ICaptureDeviceRepository _deviceRepo;
    private readonly IPacketCaptureAnalyzer _analyzer;

    public PacketCaptureController(
        IPacketCaptureService captureService,
        ICaptureDeviceRepository deviceRepo,
        IPacketCaptureAnalyzer analyzer)
    {
        _captureService = captureService;
        _deviceRepo = deviceRepo;
        _analyzer = analyzer;
    }

    [HttpGet("devices")]
    public async Task<IActionResult> ListDevices()
    {
        var devices = await _deviceRepo.GetAllAsync();
        return Ok(devices.Select(d => new CaptureDeviceDto
        {
            Id = d.Id,
            Name = d.Name,
            DisplayName = d.DisplayName,
            IpAddress = d.IpAddress,
            MacAddress = d.MacAddress
        }));
    }

    [HttpGet("devices/{id}")]
    public async Task<IActionResult> GetDevice(Guid id)
    {
        var device = await _deviceRepo.GetByIdAsync(id);
        if (device == null) return NotFound();
        return Ok(new CaptureDeviceDto
        {
            Id = device.Id,
            Name = device.Name,
            DisplayName = device.DisplayName,
            IpAddress = device.IpAddress,
            MacAddress = device.MacAddress
        });
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartCapture([FromBody] StartCaptureRequest request, CancellationToken ct = default)
    {
        await _captureService.StartCaptureAsync(request.DeviceId, ct);
        return Ok(new { started = true, deviceId = request.DeviceId });
    }

    [HttpPost("stop")]
    public async Task<IActionResult> StopCapture()
    {
        await _captureService.StopCaptureAsync();
        return Ok(new { stopped = true });
    }

    [HttpGet("status")]
    public IActionResult GetStatus() => Ok(new { isCapturing = false });

    [HttpGet("packets")]
    public async Task<IActionResult> GetPackets([FromQuery] PacketFilterDto filter, CancellationToken ct = default)
    {
        var packetFilterDto = new IoTSpy.Core.Interfaces.PacketFilterDto
        {
            Protocol = filter.Protocol,
            SourceIp = filter.SourceIp,
            DestinationIp = filter.DestinationIp,
            SourcePort = filter.SourcePort,
            DestinationPort = filter.DestinationPort,
            MacAddress = filter.MacAddress,
            ShowOnlyErrors = filter.ShowOnlyErrors,
            ShowOnlyRetransmissions = filter.ShowOnlyRetransmissions,
            FromTime = filter.FromTime.HasValue ? filter.FromTime.Value.ToUniversalTime() : null,
            ToTime = filter.ToTime.HasValue ? filter.ToTime.Value.ToUniversalTime() : null,
            PayloadSearch = filter.PayloadSearch,
            Limit = filter.Limit
        };

        var result = await _captureService.FilterPacketsAsync(packetFilterDto, ct);
        return Ok(result.Select(p => new CapturedPacketDto
        {
            Id = p.Id,
            Timestamp = p.Timestamp,
            Protocol = p.Protocol,
            SourceIp = p.SourceIp,
            DestinationIp = p.DestinationIp,
            SourcePort = p.SourcePort,
            DestinationPort = p.DestinationPort,
            Length = p.Length,
            PayloadPreview = p.PayloadPreview,
            IsError = p.IsError,
            IsRetransmission = p.IsRetransmission
        }));
    }

    [HttpGet("packets/{id}")]
    public async Task<IActionResult> GetPacket(Guid id, CancellationToken ct = default)
    {
        var packet = await _captureService.GetPacketByIdAsync(id, ct);
        if (packet == null) return NotFound();
        return Ok(new CapturedPacketDto
        {
            Id = packet.Id,
            Timestamp = packet.Timestamp,
            Protocol = packet.Protocol,
            SourceIp = packet.SourceIp,
            DestinationIp = packet.DestinationIp,
            SourcePort = packet.SourcePort,
            DestinationPort = packet.DestinationPort,
            Length = packet.Length,
            PayloadPreview = packet.PayloadPreview,
            IsError = packet.IsError,
            IsRetransmission = packet.IsRetransmission
        });
    }

    [HttpPost("packets/{id}/freeze")]
    public async Task<IActionResult> FreezePacket(Guid id, CancellationToken ct = default)
    {
        var frame = await _captureService.FreezeFrameAsync(id, ct);
        if (frame == null) return NotFound();
        return Ok(new FreezeFrameDto
        {
            PacketId = frame.PacketId,
            Timestamp = frame.Timestamp,
            FullPayloadHex = frame.FullPayloadHex,
            HexDump = frame.HexDump,
            ProtocolDetails = frame.ProtocolDetails,
            Layer2Info = frame.Layer2Info,
            Layer3Info = frame.Layer3Info,
            Layer4Info = frame.Layer4Info
        });
    }

    [HttpGet("packets/{id}/freeze")]
    public async Task<IActionResult> GetFreezeFrame(Guid id, CancellationToken ct = default)
    {
        var frame = await _captureService.GetFreezeFrameAsync(id, ct);
        if (frame == null) return NotFound();
        return Ok(new FreezeFrameDto
        {
            PacketId = frame.PacketId,
            Timestamp = frame.Timestamp,
            FullPayloadHex = frame.FullPayloadHex,
            HexDump = frame.HexDump,
            ProtocolDetails = frame.ProtocolDetails,
            Layer2Info = frame.Layer2Info,
            Layer3Info = frame.Layer3Info,
            Layer4Info = frame.Layer4Info
        });
    }

    [HttpPost("packets/{id}/delete")]
    public async Task<IActionResult> DeletePacket(Guid id, CancellationToken ct = default)
    {
        await _captureService.DeletePacketAsync(id, ct);
        return Ok(new { deleted = true, packetId = id });
    }

    [HttpGet("analysis/protocols")]
    public async Task<IActionResult> GetProtocolDistribution(CancellationToken ct = default)
    {
        var distribution = await _analyzer.AnalyzeProtocolsAsync(ct);
        if (distribution == null) return NotFound();
        return Ok(new ProtocolDistributionDto
        {
            TotalPackets = distribution.TotalPackets,
            ByProtocol = distribution.ByProtocol.Select(p => new ProtocolStatsDto
            {
                Name = p.Name,
                Count = p.Count,
                Percentage = p.Percentage
            }).ToList(),
            ByLayer3 = distribution.ByLayer3.Select(p => new ProtocolStatsDto
            {
                Name = p.Name,
                Count = p.Count,
                Percentage = p.Percentage
            }).ToList(),
            ByLayer4 = distribution.ByLayer4.Select(p => new ProtocolStatsDto
            {
                Name = p.Name,
                Count = p.Count,
                Percentage = p.Percentage
            }).ToList()
        });
    }

    [HttpGet("analysis/patterns")]
    public async Task<IActionResult> GetCommunicationPatterns([FromQuery] int topN = 10, CancellationToken ct = default)
    {
        var patterns = await _analyzer.FindCommunicationPatternsAsync(topN, ct);
        return Ok(patterns.Select(p => new CommunicationPatternDto
        {
            SourceIp = p.SourceIp,
            DestinationIp = p.DestinationIp,
            PacketCount = p.PacketCount,
            TotalBytes = p.TotalBytes,
            ProtocolsUsed = p.ProtocolsUsed,
            FirstSeen = p.FirstSeen,
            LastSeen = p.LastSeen
        }));
    }

    [HttpGet("analysis/suspicious")]
    public async Task<IActionResult> GetSuspiciousActivity(CancellationToken ct = default)
    {
        var activities = await _analyzer.DetectSuspiciousActivityAsync(ct);
        return Ok(activities.Select(a => new SuspiciousActivityDto
        {
            Id = a.Id,
            Category = a.Category,
            Severity = a.Severity,
            Description = a.Description,
            SourceIp = a.SourceIp,
            DestinationIp = a.DestinationIp,
            PacketCount = a.PacketCount,
            FirstDetected = a.FirstDetected,
            Evidence = a.Evidence
        }));
    }

    [HttpPost("freeze")]
    public IActionResult FreezeFrame()
    {
        _analyzer.FreezeFrame();
        return Ok(new { frozen = true });
    }

    [HttpPost("unfreeze")]
    public IActionResult UnfreezeFrame()
    {
        _analyzer.UnfreezeFrame();
        return Ok(new { unfrozen = true });
    }

    [HttpGet("freeze/status")]
    public IActionResult GetFreezeStatus() => Ok(new { isFrozen = _analyzer.IsFrozen, filteredCount = _analyzer.FilteredPacketCount });
}

public class CaptureDeviceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public string MacAddress { get; set; } = "";
}

public class StartCaptureRequest
{
    public Guid DeviceId { get; set; }
    public string CaptureFilter { get; set; } = "";
}

public class PacketFilterDto
{
    public Guid? DeviceId { get; set; }
    public string? Protocol { get; set; }
    public string? SourceIp { get; set; }
    public string? DestinationIp { get; set; }
    public int? SourcePort { get; set; }
    public int? DestinationPort { get; set; }
    public string? MacAddress { get; set; }
    public bool ShowOnlyErrors { get; set; }
    public bool ShowOnlyRetransmissions { get; set; }
    public DateTime? FromTime { get; set; }
    public DateTime? ToTime { get; set; }
    public string? PayloadSearch { get; set; }
    public int Limit { get; set; } = 1000;
}

public class CapturedPacketDto
{
    public Guid Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string Protocol { get; set; } = "";
    public string SourceIp { get; set; } = "";
    public string DestinationIp { get; set; } = "";
    public int? SourcePort { get; set; }
    public int? DestinationPort { get; set; }
    public int Length { get; set; }
    public string PayloadPreview { get; set; } = "";
    public bool IsError { get; set; }
    public bool IsRetransmission { get; set; }
}

public class FreezeFrameDto
{
    public Guid PacketId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string FullPayloadHex { get; set; } = "";
    public string HexDump { get; set; } = "";
    public string ProtocolDetails { get; set; } = "";
    public string Layer2Info { get; set; } = "";
    public string Layer3Info { get; set; } = "";
    public string Layer4Info { get; set; } = "";
}

public class ProtocolDistributionDto
{
    public int TotalPackets { get; set; }
    public List<ProtocolStatsDto> ByProtocol { get; set; } = new();
    public List<ProtocolStatsDto> ByLayer3 { get; set; } = new();
    public List<ProtocolStatsDto> ByLayer4 { get; set; } = new();
}

public class ProtocolStatsDto
{
    public string Name { get; set; } = "";
    public int Count { get; set; }
    public double Percentage { get; set; }
}

public class CommunicationPatternDto
{
    public string SourceIp { get; set; } = "";
    public string DestinationIp { get; set; } = "";
    public int PacketCount { get; set; }
    public long TotalBytes { get; set; }
    public List<string> ProtocolsUsed { get; set; } = new();
    public DateTimeOffset? FirstSeen { get; set; }
    public DateTimeOffset? LastSeen { get; set; }
}

public class SuspiciousActivityDto
{
    public Guid Id { get; set; }
    public string Category { get; set; } = "";
    public string Severity { get; set; } = "Medium";
    public string Description { get; set; } = "";
    public string SourceIp { get; set; } = "";
    public string? DestinationIp { get; set; }
    public int PacketCount { get; set; }
    public DateTimeOffset FirstDetected { get; set; }
    public List<string> Evidence { get; set; } = new();
}
