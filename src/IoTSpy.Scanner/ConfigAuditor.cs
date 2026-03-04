using System.Net.Sockets;
using System.Text;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Models;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Scanner;

public class ConfigAuditor(ILogger<ConfigAuditor> logger)
{
    public async Task<List<ScanFinding>> AuditAsync(
        string targetIp,
        List<ScanFinding> openPorts,
        int timeoutMs,
        CancellationToken ct = default)
    {
        var findings = new List<ScanFinding>();
        var services = openPorts
            .Where(f => f is { Port: not null, ServiceName: not null })
            .ToDictionary(f => f.ServiceName!, f => f.Port!.Value);

        // Check for Telnet open (always a risk on IoT)
        if (services.ContainsKey("telnet"))
        {
            findings.Add(new ScanFinding
            {
                Type = ScanFindingType.ConfigIssue,
                Severity = ScanFindingSeverity.High,
                Title = "Telnet service is open",
                Description = "Telnet transmits credentials in plaintext. This is a significant security risk, especially on IoT devices. Disable Telnet and use SSH instead.",
                Port = services["telnet"],
                Protocol = "tcp",
                ServiceName = "telnet"
            });
        }

        // Check for anonymous MQTT
        if (services.TryGetValue("mqtt", out var mqttPort))
        {
            var anonMqtt = await CheckAnonymousMqttAsync(targetIp, mqttPort, timeoutMs, ct);
            if (anonMqtt)
            {
                findings.Add(new ScanFinding
                {
                    Type = ScanFindingType.ConfigIssue,
                    Severity = ScanFindingSeverity.High,
                    Title = "MQTT broker allows anonymous access",
                    Description = "The MQTT broker accepts connections without authentication. An attacker can subscribe to all topics and publish arbitrary messages.",
                    Port = mqttPort,
                    Protocol = "tcp",
                    ServiceName = "mqtt"
                });
            }
        }

        // Check for HTTP admin on default port
        if (services.TryGetValue("http", out var httpPort))
        {
            var httpAdmin = await CheckHttpAdminAsync(targetIp, httpPort, timeoutMs, ct);
            if (httpAdmin is not null)
                findings.Add(httpAdmin);
        }
        if (services.TryGetValue("http-alt", out var httpAltPort))
        {
            var httpAdmin = await CheckHttpAdminAsync(targetIp, httpAltPort, timeoutMs, ct);
            if (httpAdmin is not null)
                findings.Add(httpAdmin);
        }

        // Check for UPnP (typically port 1900 SSDP, but also check TCP)
        var upnpPort = openPorts.FirstOrDefault(f => f.Port == 1900 || f.Port == 5000 || f.Port == 49152);
        if (upnpPort is not null)
        {
            findings.Add(new ScanFinding
            {
                Type = ScanFindingType.ConfigIssue,
                Severity = ScanFindingSeverity.Medium,
                Title = "UPnP service detected",
                Description = "UPnP is enabled and responding. UPnP can be exploited for unauthorized port forwarding, NAT traversal attacks, and device discovery. Disable if not required.",
                Port = upnpPort.Port,
                Protocol = "tcp",
                ServiceName = "upnp"
            });
        }

        // Check for FTP without TLS
        if (services.ContainsKey("ftp"))
        {
            findings.Add(new ScanFinding
            {
                Type = ScanFindingType.ConfigIssue,
                Severity = ScanFindingSeverity.Medium,
                Title = "FTP service is open (no encryption)",
                Description = "FTP transmits data and credentials in plaintext. Use SFTP or FTPS instead.",
                Port = services["ftp"],
                Protocol = "tcp",
                ServiceName = "ftp"
            });
        }

        // Check for exposed databases
        foreach (var dbService in new[] { "redis", "mysql", "postgres", "mongodb" })
        {
            if (services.TryGetValue(dbService, out var dbPort))
            {
                findings.Add(new ScanFinding
                {
                    Type = ScanFindingType.ConfigIssue,
                    Severity = ScanFindingSeverity.High,
                    Title = $"{dbService} database exposed on port {dbPort}",
                    Description = $"The {dbService} database service is accessible on port {dbPort}. Database services should not be directly exposed on IoT devices.",
                    Port = dbPort,
                    Protocol = "tcp",
                    ServiceName = dbService
                });
            }
        }

        logger.LogInformation("Config audit of {Target}: {Count} issues found", targetIp, findings.Count);
        return findings;
    }

    private static async Task<bool> CheckAnonymousMqttAsync(
        string host, int port, int timeoutMs, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            await client.ConnectAsync(host, port, cts.Token);

            var stream = client.GetStream();

            // MQTT CONNECT with no username/password (anonymous)
            byte[] connectPacket =
            [
                0x10, 0x11, // Fixed header: CONNECT, remaining length=17
                0x00, 0x04, 0x4D, 0x51, 0x54, 0x54, // Protocol Name "MQTT"
                0x04, // Protocol Level 4 (3.1.1)
                0x02, // Connect Flags: Clean Session only
                0x00, 0x3C, // Keep Alive: 60s
                0x00, 0x07, 0x69, 0x6F, 0x74, 0x73, 0x70, 0x79, 0x61 // Client ID "iotspya"
            ];

            await stream.WriteAsync(connectPacket, cts.Token);

            var buffer = new byte[256];
            var read = await stream.ReadAsync(buffer, cts.Token);

            // CONNACK: 0x20 type, byte[3] == 0x00 means connection accepted
            return read >= 4 && buffer[0] == 0x20 && buffer[3] == 0x00;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<ScanFinding?> CheckHttpAdminAsync(
        string host, int port, int timeoutMs, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            await client.ConnectAsync(host, port, cts.Token);

            var stream = client.GetStream();
            var request = $"GET / HTTP/1.0\r\nHost: {host}\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(request), cts.Token);

            var buffer = new byte[4096];
            var read = await stream.ReadAsync(buffer, cts.Token);
            var response = Encoding.ASCII.GetString(buffer, 0, read).ToLowerInvariant();

            // Look for common admin interface indicators
            if (response.Contains("admin") || response.Contains("login") ||
                response.Contains("configuration") || response.Contains("management") ||
                response.Contains("dashboard") || response.Contains("setup"))
            {
                return new ScanFinding
                {
                    Type = ScanFindingType.ConfigIssue,
                    Severity = ScanFindingSeverity.Medium,
                    Title = $"HTTP admin interface on port {port}",
                    Description = $"An HTTP admin/management interface was detected on port {port}. Ensure it requires strong authentication and is not using default credentials.",
                    Port = port,
                    Protocol = "tcp",
                    ServiceName = "http"
                };
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
