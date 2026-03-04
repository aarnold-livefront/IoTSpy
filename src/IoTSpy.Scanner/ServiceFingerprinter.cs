using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Models;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Scanner;

public partial class ServiceFingerprinter(ILogger<ServiceFingerprinter> logger)
{
    private static readonly Dictionary<string, byte[]> ServiceProbes = new()
    {
        ["http"] = "GET / HTTP/1.0\r\nHost: target\r\n\r\n"u8.ToArray(),
        ["smtp"] = []  , // SMTP sends banner on connect
        ["ftp"] = [],    // FTP sends banner on connect
        ["ssh"] = [],    // SSH sends banner on connect
        ["mqtt"] = [0x10, 0x0E, 0x00, 0x04, 0x4D, 0x51, 0x54, 0x54, 0x04, 0x02, 0x00, 0x3C, 0x00, 0x02, 0x69, 0x64], // MQTT CONNECT
    };

    public async Task<List<ScanFinding>> FingerprintAsync(
        string targetIp,
        List<ScanFinding> openPorts,
        int timeoutMs,
        CancellationToken ct = default)
    {
        var findings = new List<ScanFinding>();

        foreach (var portFinding in openPorts)
        {
            ct.ThrowIfCancellationRequested();
            if (portFinding.Port is not { } port) continue;

            try
            {
                var banner = await GrabBannerAsync(targetIp, port, portFinding.ServiceName, timeoutMs, ct);
                if (string.IsNullOrWhiteSpace(banner)) continue;

                var serviceName = portFinding.ServiceName ?? IdentifyService(banner, port);
                var cpe = ExtractCpe(banner, serviceName);

                findings.Add(new ScanFinding
                {
                    Type = ScanFindingType.ServiceBanner,
                    Severity = ScanFindingSeverity.Info,
                    Title = $"Service identified on port {port}: {serviceName ?? "unknown"}",
                    Description = $"Banner: {Truncate(banner, 500)}",
                    Port = port,
                    Protocol = "tcp",
                    ServiceName = serviceName,
                    Banner = Truncate(banner, 2000),
                    Cpe = cpe
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug(ex, "Failed to fingerprint {Target}:{Port}", targetIp, port);
            }
        }

        logger.LogInformation("Fingerprinted {Count} services on {Target}", findings.Count, targetIp);
        return findings;
    }

    private static async Task<string?> GrabBannerAsync(
        string targetIp, int port, string? serviceName, int timeoutMs, CancellationToken ct)
    {
        using var client = new TcpClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        await client.ConnectAsync(targetIp, port, cts.Token);
        var stream = client.GetStream();

        // Some services send a banner immediately on connect (SSH, FTP, SMTP)
        // Others need a probe (HTTP, MQTT)
        byte[]? probe = null;
        if (serviceName is not null)
            ServiceProbes.TryGetValue(serviceName, out probe);

        if (probe is { Length: > 0 })
        {
            await stream.WriteAsync(probe, cts.Token);
        }

        var buffer = new byte[4096];

        // Wait briefly for banner
        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
        readCts.CancelAfter(Math.Min(timeoutMs, 3000));

        try
        {
            var bytesRead = await stream.ReadAsync(buffer, readCts.Token);
            if (bytesRead > 0)
                return Encoding.UTF8.GetString(buffer, 0, bytesRead).TrimEnd('\0');
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Read timeout — no banner available
        }

        return null;
    }

    private static string? IdentifyService(string banner, int port)
    {
        if (banner.StartsWith("SSH-", StringComparison.Ordinal)) return "ssh";
        if (banner.StartsWith("220", StringComparison.Ordinal) && banner.Contains("FTP", StringComparison.OrdinalIgnoreCase)) return "ftp";
        if (banner.StartsWith("220", StringComparison.Ordinal) && banner.Contains("SMTP", StringComparison.OrdinalIgnoreCase)) return "smtp";
        if (banner.StartsWith("HTTP/", StringComparison.Ordinal)) return "http";
        if (banner.StartsWith("+OK", StringComparison.Ordinal)) return "pop3";
        if (banner.Contains("IMAP", StringComparison.OrdinalIgnoreCase)) return "imap";
        if (banner.Contains("redis", StringComparison.OrdinalIgnoreCase)) return "redis";
        if (banner.Contains("MySQL", StringComparison.OrdinalIgnoreCase)) return "mysql";
        if (port == 1883 || port == 8883) return "mqtt";
        if (port == 23) return "telnet";
        return null;
    }

    internal static string? ExtractCpe(string banner, string? serviceName)
    {
        // SSH: "SSH-2.0-OpenSSH_8.9p1 Ubuntu-3ubuntu0.1"
        var sshMatch = SshBannerRegex().Match(banner);
        if (sshMatch.Success)
        {
            var version = sshMatch.Groups[1].Value.Replace('p', '.');
            return $"cpe:2.3:a:openbsd:openssh:{version}:*:*:*:*:*:*:*";
        }

        // HTTP Server header: "Server: Apache/2.4.52" or "Server: nginx/1.18.0"
        var serverMatch = HttpServerRegex().Match(banner);
        if (serverMatch.Success)
        {
            var product = serverMatch.Groups[1].Value.ToLowerInvariant();
            var version = serverMatch.Groups[2].Value;
            var vendor = product switch
            {
                "apache" => "apache",
                "nginx" => "nginx",
                "lighttpd" => "lighttpd",
                _ => product
            };
            return $"cpe:2.3:a:{vendor}:{product}:{version}:*:*:*:*:*:*:*";
        }

        // Dropbear SSH: "SSH-2.0-dropbear_2022.83"
        var dropbearMatch = DropbearRegex().Match(banner);
        if (dropbearMatch.Success)
        {
            var version = dropbearMatch.Groups[1].Value;
            return $"cpe:2.3:a:dropbear_ssh_project:dropbear_ssh:{version}:*:*:*:*:*:*:*";
        }

        return null;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";

    [GeneratedRegex(@"SSH-2\.0-OpenSSH_(\d+\.\d+(?:p\d+)?)", RegexOptions.Compiled)]
    private static partial Regex SshBannerRegex();

    [GeneratedRegex(@"Server:\s*(\w+)/([\d.]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex HttpServerRegex();

    [GeneratedRegex(@"dropbear_(\d+\.\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex DropbearRegex();
}
