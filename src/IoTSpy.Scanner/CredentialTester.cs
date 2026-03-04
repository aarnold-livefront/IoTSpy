using System.Net.Sockets;
using System.Text;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Models;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Scanner;

public class CredentialTester(ILogger<CredentialTester> logger)
{
    private static readonly Dictionary<string, List<(string User, string Pass)>> DefaultCredentials = new()
    {
        ["ssh"] =
        [
            ("root", "root"), ("root", "admin"), ("root", "password"), ("root", "1234"),
            ("admin", "admin"), ("admin", "password"), ("admin", "1234"),
            ("user", "user"), ("pi", "raspberry")
        ],
        ["telnet"] =
        [
            ("root", "root"), ("root", "admin"), ("root", ""), ("root", "password"),
            ("admin", "admin"), ("admin", "password"), ("admin", "1234"), ("admin", ""),
            ("user", "user"), ("guest", "guest")
        ],
        ["ftp"] =
        [
            ("anonymous", ""), ("anonymous", "anonymous@"), ("ftp", "ftp"),
            ("admin", "admin"), ("root", "root"), ("admin", "password")
        ],
        ["mqtt"] =
        [
            ("", ""), // anonymous access
            ("admin", "admin"), ("admin", "password"), ("mqtt", "mqtt"),
            ("guest", "guest"), ("user", "password")
        ],
        ["http"] =
        [
            ("admin", "admin"), ("admin", "password"), ("admin", "1234"),
            ("root", "root"), ("admin", ""), ("user", "user")
        ]
    };

    public async Task<List<ScanFinding>> TestAsync(
        string targetIp,
        List<ScanFinding> openPorts,
        int timeoutMs,
        CancellationToken ct = default)
    {
        var findings = new List<ScanFinding>();

        foreach (var portFinding in openPorts)
        {
            ct.ThrowIfCancellationRequested();
            if (portFinding is not { Port: { } port, ServiceName: { } service }) continue;
            if (!DefaultCredentials.TryGetValue(service, out var creds)) continue;

            foreach (var (user, pass) in creds)
            {
                ct.ThrowIfCancellationRequested();

                var success = service switch
                {
                    "ftp" => await TestFtpAsync(targetIp, port, user, pass, timeoutMs, ct),
                    "telnet" => await TestTelnetAsync(targetIp, port, user, pass, timeoutMs, ct),
                    "mqtt" => await TestMqttAsync(targetIp, port, user, pass, timeoutMs, ct),
                    _ => false
                };

                if (success)
                {
                    var displayPass = string.IsNullOrEmpty(pass) ? "(empty)" : pass;
                    var displayUser = string.IsNullOrEmpty(user) ? "(anonymous)" : user;

                    findings.Add(new ScanFinding
                    {
                        Type = ScanFindingType.DefaultCredential,
                        Severity = ScanFindingSeverity.Critical,
                        Title = $"Default credentials on {service}:{port}",
                        Description = $"Service {service} on port {port} accepts default credentials {displayUser}:{displayPass}",
                        Port = port,
                        Protocol = "tcp",
                        ServiceName = service,
                        Username = user,
                        Password = pass
                    });

                    // One set of working creds per service is enough
                    break;
                }
            }
        }

        logger.LogInformation("Credential testing on {Target}: {Count} default credential findings",
            targetIp, findings.Count);
        return findings;
    }

    private static async Task<bool> TestFtpAsync(
        string host, int port, string user, string pass, int timeoutMs, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            await client.ConnectAsync(host, port, cts.Token);

            var stream = client.GetStream();
            var buffer = new byte[1024];

            // Read welcome banner
            await stream.ReadAsync(buffer, cts.Token);

            // Send USER
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"USER {user}\r\n"), cts.Token);
            var read = await stream.ReadAsync(buffer, cts.Token);
            var response = Encoding.ASCII.GetString(buffer, 0, read);

            if (response.StartsWith("230", StringComparison.Ordinal))
                return true; // No password needed

            if (!response.StartsWith("331", StringComparison.Ordinal))
                return false;

            // Send PASS
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"PASS {pass}\r\n"), cts.Token);
            read = await stream.ReadAsync(buffer, cts.Token);
            response = Encoding.ASCII.GetString(buffer, 0, read);

            return response.StartsWith("230", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TestTelnetAsync(
        string host, int port, string user, string pass, int timeoutMs, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs * 2); // Telnet negotiation can be slow
            await client.ConnectAsync(host, port, cts.Token);

            var stream = client.GetStream();
            var buffer = new byte[4096];

            // Read initial negotiation / banner
            await Task.Delay(500, cts.Token);
            if (stream.DataAvailable)
                await stream.ReadAsync(buffer, cts.Token);

            // Send username
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"{user}\r\n"), cts.Token);
            await Task.Delay(500, cts.Token);
            if (stream.DataAvailable)
                await stream.ReadAsync(buffer, cts.Token);

            // Send password
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"{pass}\r\n"), cts.Token);
            await Task.Delay(1000, cts.Token);

            if (!stream.DataAvailable) return false;
            var read = await stream.ReadAsync(buffer, cts.Token);
            var response = Encoding.ASCII.GetString(buffer, 0, read);

            // Look for shell prompt indicators (success) or login failure messages
            return (response.Contains('$') || response.Contains('#') || response.Contains('>'))
                && !response.Contains("incorrect", StringComparison.OrdinalIgnoreCase)
                && !response.Contains("failed", StringComparison.OrdinalIgnoreCase)
                && !response.Contains("denied", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TestMqttAsync(
        string host, int port, string user, string pass, int timeoutMs, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            await client.ConnectAsync(host, port, cts.Token);

            var stream = client.GetStream();
            var connectPacket = BuildMqttConnect(user, pass);
            await stream.WriteAsync(connectPacket, cts.Token);

            var buffer = new byte[256];
            var read = await stream.ReadAsync(buffer, cts.Token);

            // CONNACK: byte 0 = 0x20 (CONNACK type), byte 3 = return code (0 = accepted)
            return read >= 4 && buffer[0] == 0x20 && buffer[3] == 0x00;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] BuildMqttConnect(string username, string password)
    {
        var payload = new List<byte>();

        // Protocol Name
        payload.AddRange(MqttString("MQTT"));
        // Protocol Level (4 = MQTT 3.1.1)
        payload.Add(0x04);

        byte connectFlags = 0x02; // Clean Session
        if (!string.IsNullOrEmpty(username))
            connectFlags |= 0x80; // Username flag
        if (!string.IsNullOrEmpty(password))
            connectFlags |= 0x40; // Password flag
        payload.Add(connectFlags);

        // Keep Alive (60 seconds)
        payload.Add(0x00);
        payload.Add(0x3C);

        // Client ID
        payload.AddRange(MqttString("iotspy-scan"));

        if (!string.IsNullOrEmpty(username))
            payload.AddRange(MqttString(username));
        if (!string.IsNullOrEmpty(password))
            payload.AddRange(MqttString(password));

        // Fixed header: CONNECT (0x10) + remaining length
        var packet = new List<byte> { 0x10 };
        var remaining = payload.Count;
        do
        {
            var encodedByte = (byte)(remaining % 128);
            remaining /= 128;
            if (remaining > 0) encodedByte |= 0x80;
            packet.Add(encodedByte);
        } while (remaining > 0);

        packet.AddRange(payload);
        return [.. packet];
    }

    private static byte[] MqttString(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var result = new byte[bytes.Length + 2];
        result[0] = (byte)(bytes.Length >> 8);
        result[1] = (byte)(bytes.Length & 0xFF);
        Array.Copy(bytes, 0, result, 2, bytes.Length);
        return result;
    }
}
