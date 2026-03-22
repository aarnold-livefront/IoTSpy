using IoTSpy.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTSpy.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportController(IReportService reportService, IDeviceRepository devices) : ControllerBase
{
    [HttpGet("devices/{deviceId:guid}/html")]
    public async Task<IActionResult> GetHtmlReport(Guid deviceId, CancellationToken ct)
    {
        var device = await devices.GetByIdAsync(deviceId, ct);
        if (device is null) return NotFound();

        var html = await reportService.GenerateHtmlReportAsync(deviceId, ct);
        return File(html, "text/html", $"scan-report-{deviceId}.html");
    }

    [HttpGet("devices/{deviceId:guid}/pdf")]
    public async Task<IActionResult> GetPdfReport(Guid deviceId, CancellationToken ct)
    {
        var device = await devices.GetByIdAsync(deviceId, ct);
        if (device is null) return NotFound();

        var pdf = await reportService.GeneratePdfReportAsync(deviceId, ct);
        return File(pdf, "application/pdf", $"scan-report-{deviceId}.pdf");
    }
}
