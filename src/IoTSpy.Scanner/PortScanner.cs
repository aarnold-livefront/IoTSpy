using System.Net.Sockets;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Models;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Scanner;

public class PortScanner(ILogger<PortScanner> logger)
{
    public async Task<List<ScanFinding>> ScanAsync(
        string targetIp,
        string portRange,
        int maxConcurrency,
        int timeoutMs,
        CancellationToken ct = default)
    {
        var ports = ParsePortRange(portRange);
        var findings = new List<ScanFinding>();
        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var tasks = new List<Task<ScanFinding?>>();

        foreach (var port in ports)
        {
            ct.ThrowIfCancellationRequested();
            await semaphore.WaitAsync(ct);

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    return await ProbePortAsync(targetIp, port, timeoutMs, ct);
                }
                finally
                {
                    semaphore.Release();
                }
            }, ct));
        }

        var results = await Task.WhenAll(tasks);
        foreach (var result in results)
        {
            if (result is not null)
                findings.Add(result);
        }

        logger.LogInformation("Port scan of {Target} complete: {OpenPorts} open ports found across {Total} probed",
            targetIp, findings.Count, ports.Count);

        return findings;
    }

    private static async Task<ScanFinding?> ProbePortAsync(
        string targetIp, int port, int timeoutMs, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);

            await client.ConnectAsync(targetIp, port, cts.Token);

            return new ScanFinding
            {
                Type = ScanFindingType.OpenPort,
                Severity = ScanFindingSeverity.Info,
                Title = $"Open port {port}/tcp",
                Description = $"TCP port {port} is open on {targetIp}",
                Port = port,
                Protocol = "tcp",
                ServiceName = WellKnownService(port)
            };
        }
        catch
        {
            return null;
        }
    }

    internal static List<int> ParsePortRange(string portRange)
    {
        var ports = new HashSet<int>();

        foreach (var segment in portRange.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (segment.Contains('-'))
            {
                var parts = segment.Split('-', 2);
                if (int.TryParse(parts[0], out var start) && int.TryParse(parts[1], out var end))
                {
                    for (var p = Math.Max(1, start); p <= Math.Min(65535, end); p++)
                        ports.Add(p);
                }
            }
            else if (int.TryParse(segment, out var single) && single is >= 1 and <= 65535)
            {
                ports.Add(single);
            }
        }

        return [.. ports.Order()];
    }

    private static string? WellKnownService(int port) => port switch
    {
        21 => "ftp",
        22 => "ssh",
        23 => "telnet",
        25 => "smtp",
        53 => "dns",
        80 => "http",
        110 => "pop3",
        143 => "imap",
        443 => "https",
        445 => "smb",
        502 => "modbus",
        554 => "rtsp",
        1883 => "mqtt",
        3306 => "mysql",
        5432 => "postgres",
        5683 => "coap",
        6379 => "redis",
        8080 => "http-alt",
        8443 => "https-alt",
        8883 => "mqtt-tls",
        9100 => "jetdirect",
        27017 => "mongodb",
        _ => null
    };
}
