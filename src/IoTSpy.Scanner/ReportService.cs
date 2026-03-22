using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;

namespace IoTSpy.Scanner;

/// <summary>
/// Generates HTML and PDF scan reports for a device.
/// </summary>
public sealed class ReportService : IReportService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReportService> _logger;

    public ReportService(IServiceScopeFactory scopeFactory, ILogger<ReportService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> GenerateHtmlReportAsync(Guid deviceId, CancellationToken ct = default)
    {
        var (device, jobs, findings) = await LoadDataAsync(deviceId, ct);
        var html = BuildHtml(device, jobs, findings);
        return Encoding.UTF8.GetBytes(html);
    }

    public async Task<byte[]> GeneratePdfReportAsync(Guid deviceId, CancellationToken ct = default)
    {
        var (device, jobs, findings) = await LoadDataAsync(deviceId, ct);
        return BuildPdf(device, jobs, findings);
    }

    // ── Data loading ──────────────────────────────────────────────────────────

    private async Task<(Device? device, List<ScanJob> jobs, List<ScanFinding> findings)> LoadDataAsync(
        Guid deviceId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var deviceRepo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
        var scanJobRepo = scope.ServiceProvider.GetRequiredService<IScanJobRepository>();

        var device = await deviceRepo.GetByIdAsync(deviceId, ct);
        var jobs = await scanJobRepo.GetByDeviceIdAsync(deviceId, ct);

        var findings = new List<ScanFinding>();
        foreach (var job in jobs)
        {
            var jobFindings = await scanJobRepo.GetFindingsAsync(job.Id, ct);
            findings.AddRange(jobFindings);
        }

        return (device, jobs, findings);
    }

    // ── HTML generation ───────────────────────────────────────────────────────

    private static string BuildHtml(Device? device, List<ScanJob> jobs, List<ScanFinding> findings)
    {
        var deviceName = device is not null
            ? $"{device.Label} ({device.IpAddress})"
            : "Unknown Device";

        var grouped = new[]
        {
            ScanFindingSeverity.Critical,
            ScanFindingSeverity.High,
            ScanFindingSeverity.Medium,
            ScanFindingSeverity.Low,
            ScanFindingSeverity.Info
        };

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\"><head><meta charset=\"UTF-8\">");
        sb.AppendLine("<title>IoTSpy Scan Report</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:sans-serif;margin:2rem;color:#222}");
        sb.AppendLine("h1{color:#1a73e8}h2{margin-top:2rem}");
        sb.AppendLine("table{border-collapse:collapse;width:100%}");
        sb.AppendLine("th,td{border:1px solid #ccc;padding:6px 10px;text-align:left}");
        sb.AppendLine("th{background:#f0f0f0}");
        sb.AppendLine(".Critical{color:#b00020}.High{color:#e65100}.Medium{color:#f57c00}.Low{color:#388e3c}.Info{color:#1565c0}");
        sb.AppendLine(".badge{display:inline-block;padding:2px 8px;border-radius:4px;font-size:.85em;font-weight:bold}");
        sb.AppendLine(".badge-Critical{background:#fdecea;color:#b00020}.badge-High{background:#fff3e0;color:#e65100}");
        sb.AppendLine(".badge-Medium{background:#fff8e1;color:#f57c00}.badge-Low{background:#e8f5e9;color:#388e3c}");
        sb.AppendLine(".badge-Info{background:#e3f2fd;color:#1565c0}");
        sb.AppendLine("</style></head><body>");

        sb.AppendLine($"<h1>IoTSpy Security Scan Report</h1>");
        sb.AppendLine($"<p><strong>Device:</strong> {Escape(deviceName)}</p>");
        sb.AppendLine($"<p><strong>Generated:</strong> {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>");
        sb.AppendLine($"<p><strong>Total Scan Jobs:</strong> {jobs.Count}</p>");
        sb.AppendLine($"<p><strong>Total Findings:</strong> {findings.Count}</p>");

        if (device is not null)
        {
            sb.AppendLine("<h2>Device Information</h2><table>");
            sb.AppendLine($"<tr><th>IP Address</th><td>{Escape(device.IpAddress)}</td></tr>");
            sb.AppendLine($"<tr><th>MAC Address</th><td>{Escape(device.MacAddress)}</td></tr>");
            sb.AppendLine($"<tr><th>Hostname</th><td>{Escape(device.Hostname)}</td></tr>");
            sb.AppendLine($"<tr><th>Vendor</th><td>{Escape(device.Vendor)}</td></tr>");
            sb.AppendLine($"<tr><th>Security Score</th><td>{(device.SecurityScore == -1 ? "Unscored" : device.SecurityScore)}</td></tr>");
            sb.AppendLine("</table>");
        }

        sb.AppendLine("<h2>Findings by Severity</h2>");

        foreach (var severity in grouped)
        {
            var group = findings.Where(f => f.Severity == severity).ToList();
            sb.AppendLine($"<h3><span class=\"badge badge-{severity}\">{severity}</span> ({group.Count} findings)</h3>");
            if (group.Count == 0)
            {
                sb.AppendLine("<p><em>No findings.</em></p>");
                continue;
            }
            sb.AppendLine("<table><tr><th>Type</th><th>Title</th><th>Description</th><th>Port</th><th>CVE</th><th>Found At</th></tr>");
            foreach (var f in group)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{Escape(f.Type.ToString())}</td>");
                sb.AppendLine($"<td>{Escape(f.Title)}</td>");
                sb.AppendLine($"<td>{Escape(f.Description)}</td>");
                sb.AppendLine($"<td>{(f.Port.HasValue ? f.Port.Value.ToString() : "")}</td>");
                sb.AppendLine($"<td>{Escape(f.CveId ?? "")}</td>");
                sb.AppendLine($"<td>{f.FoundAt:yyyy-MM-dd HH:mm}</td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</table>");
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    // ── PDF generation ────────────────────────────────────────────────────────

    private static byte[] BuildPdf(Device? device, List<ScanJob> jobs, List<ScanFinding> findings)
    {
        var deviceName = device is not null
            ? $"{device.Label} ({device.IpAddress})"
            : "Unknown Device";

        var grouped = new[]
        {
            ScanFindingSeverity.Critical,
            ScanFindingSeverity.High,
            ScanFindingSeverity.Medium,
            ScanFindingSeverity.Low,
            ScanFindingSeverity.Info
        };

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Text("IoTSpy Security Scan Report")
                    .SemiBold().FontSize(18).FontColor(Colors.Blue.Darken2);

                page.Content().Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Text($"Device: {deviceName}");
                    col.Item().Text($"Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                    col.Item().Text($"Total Scan Jobs: {jobs.Count} | Total Findings: {findings.Count}");

                    if (device is not null)
                    {
                        col.Item().Text("Device Information").SemiBold().FontSize(13);
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(2); });
                            t.Cell().Text("IP Address"); t.Cell().Text(device.IpAddress);
                            t.Cell().Text("MAC Address"); t.Cell().Text(device.MacAddress);
                            t.Cell().Text("Hostname"); t.Cell().Text(device.Hostname);
                            t.Cell().Text("Security Score"); t.Cell().Text(device.SecurityScore == -1 ? "Unscored" : device.SecurityScore.ToString());
                        });
                    }

                    foreach (var severity in grouped)
                    {
                        var group = findings.Where(f => f.Severity == severity).ToList();
                        col.Item().Text($"{severity} Findings ({group.Count})").SemiBold().FontSize(12);
                        if (group.Count == 0)
                        {
                            col.Item().Text("No findings.").Italic();
                            continue;
                        }
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2);
                                c.RelativeColumn(3);
                                c.RelativeColumn();
                            });
                            t.Header(h =>
                            {
                                h.Cell().Background(Colors.Grey.Lighten2).Text("Title").SemiBold();
                                h.Cell().Background(Colors.Grey.Lighten2).Text("Description").SemiBold();
                                h.Cell().Background(Colors.Grey.Lighten2).Text("CVE").SemiBold();
                            });
                            foreach (var f in group)
                            {
                                t.Cell().Text(f.Title);
                                t.Cell().Text(f.Description);
                                t.Cell().Text(f.CveId ?? "");
                            }
                        });
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        });

        return doc.GeneratePdf();
    }
}
