using System.Net;
using System.Net.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using IoTSpy.Proxy.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Timeout;

namespace IoTSpy.Proxy.Interception;

/// <summary>
/// HTTP/HTTPS explicit proxy server. Listens for HTTP CONNECT tunnels and plain HTTP
/// requests, performs TLS MITM when CaptureTls is enabled, and publishes every
/// captured transaction to the <see cref="ICapturePublisher"/>.
/// </summary>
public class ExplicitProxyServer(
    ICertificateAuthority ca,
    ICapturePublisher publisher,
    IServiceScopeFactory scopeFactory,
    ResiliencePipelineProvider<string> connectPipelineProvider,
    ILogger<ExplicitProxyServer> logger)
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public bool IsRunning => _listener is not null;

    public async Task StartAsync(int port, string listenAddress, CancellationToken ct = default)
    {
        if (IsRunning) return;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var ip = listenAddress is "0.0.0.0" or "*"
            ? IPAddress.Any
            : IPAddress.Parse(listenAddress);
        _listener = new TcpListener(ip, port);
        _listener.Start();
        logger.LogInformation("Explicit proxy listening on {Addr}:{Port}", listenAddress, port);
        _ = AcceptLoopAsync(_cts.Token);
    }

    public Task StopAsync()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _listener = null;
        logger.LogInformation("Explicit proxy stopped");
        return Task.CompletedTask;
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(ct);
                _ = HandleClientAsync(client, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Accept error");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            client.ReceiveTimeout = 30_000;
            client.SendTimeout = 30_000;

            try
            {
                var stream = client.GetStream();
                var clientIp = ((IPEndPoint?)client.Client.RemoteEndPoint)?.Address.ToString() ?? "unknown";
                var requestLine = await ReadLineAsync(stream, ct);
                if (string.IsNullOrEmpty(requestLine)) return;

                using var scope = scopeFactory.CreateScope();
                var settingsRepo = scope.ServiceProvider.GetRequiredService<IProxySettingsRepository>();
                var settings = await settingsRepo.GetAsync(ct);

                if (requestLine.StartsWith("CONNECT ", StringComparison.OrdinalIgnoreCase))
                    await HandleConnectAsync(stream, requestLine, clientIp, settings, scope, ct);
                else
                    await HandlePlainHttpAsync(stream, requestLine, clientIp, settings, scope, ct);
            }
            catch (BrokenCircuitException ex)
            {
                logger.LogWarning("Circuit breaker open for upstream: {Message}", ex.Message);
                await TrySend503Async(client);
            }
            catch (TimeoutRejectedException ex)
            {
                logger.LogWarning("Upstream connection timed out: {Message}", ex.Message);
                await TrySend503Async(client);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogDebug(ex, "Client handler error");
            }
        }
    }

    // ── HTTPS (CONNECT tunnel + TLS MITM) ──────────────────────────────────

    private async Task HandleConnectAsync(
        NetworkStream clientStream, string requestLine,
        string clientIp, ProxySettings settings, IServiceScope scope, CancellationToken ct)
    {
        // CONNECT example.com:443 HTTP/1.1
        var parts = requestLine.Split(' ');
        if (parts.Length < 2) return;
        var hostPort = parts[1];
        var host = hostPort.Contains(':') ? hostPort[..hostPort.LastIndexOf(':')] : hostPort;
        int.TryParse(hostPort.Contains(':') ? hostPort[(hostPort.LastIndexOf(':') + 1)..] : "443", out var port);

        // Drain remaining CONNECT headers
        while (true)
        {
            var line = await ReadLineAsync(clientStream, ct);
            if (string.IsNullOrEmpty(line)) break;
        }

        // Respond 200 Connection established
        var ok = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n");
        await clientStream.WriteAsync(ok, ct);

        if (!settings.CaptureTls)
        {
            // Passthrough without interception — resilient connect
            var upstream = new TcpClient();
            var connectPipeline = connectPipelineProvider.GetPipeline(host);
            await connectPipeline.ExecuteAsync(async token =>
            {
                await upstream.ConnectAsync(host, port, token);
                return upstream;
            }, ct);

            using (upstream)
            {
                await Task.WhenAll(
                    clientStream.CopyToAsync(upstream.GetStream(), ct),
                    upstream.GetStream().CopyToAsync(clientStream, ct));
            }
            return;
        }

        // TLS MITM
        var certEntry = await ca.GetOrCreateHostCertificateAsync(host, ct);
        var x509 = BuildX509(certEntry);

        using var sslClient = new SslStream(clientStream, leaveInnerStreamOpen: true);
        await sslClient.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
        {
            ServerCertificate = x509,
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            ClientCertificateRequired = false
        }, ct);

        // Resilient connect to upstream
        var upstreamTcp = new TcpClient();
        var mitmConnectPipeline = connectPipelineProvider.GetPipeline(host);
        await mitmConnectPipeline.ExecuteAsync(async token =>
        {
            await upstreamTcp.ConnectAsync(host, port, token);
            return upstreamTcp;
        }, ct);

        using (upstreamTcp)
        {
            using var sslUpstream = new SslStream(upstreamTcp.GetStream());
            // Resilient TLS handshake
            await connectPipelineProvider.GetPipeline(ProxyResiliencePipelines.TlsPipelineKey).ExecuteAsync(async token =>
            {
                await sslUpstream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = host,
                    RemoteCertificateValidationCallback = (_, _, _, _) => true
                }, token);
            }, ct);

            await InterceptHttpStreamAsync(sslClient, sslUpstream, host, port, "https",
                sslClient.SslProtocol.ToString(), sslClient.NegotiatedCipherSuite.ToString(),
                clientIp, settings, scope, ct);
        }
    }

    // ── Plain HTTP ──────────────────────────────────────────────────────────

    private async Task HandlePlainHttpAsync(
        NetworkStream clientStream, string requestLine,
        string clientIp, ProxySettings settings, IServiceScope scope, CancellationToken ct)
    {
        var lines = new List<string> { requestLine };
        string? hostHeader = null;
        while (true)
        {
            var line = await ReadLineAsync(clientStream, ct);
            lines.Add(line ?? "");
            if (string.IsNullOrEmpty(line)) break;
            if (line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
                hostHeader = line[5..].Trim();
        }

        if (hostHeader is null) return;
        var host = hostHeader.Contains(':') ? hostHeader[..hostHeader.LastIndexOf(':')] : hostHeader;
        int.TryParse(hostHeader.Contains(':') ? hostHeader[(hostHeader.LastIndexOf(':') + 1)..] : "80", out var port);
        if (port == 0) port = 80;

        var headerBlock = string.Join("\r\n", lines) + "\r\n";
        var headerBytes = Encoding.UTF8.GetBytes(headerBlock);

        // Resilient connect to upstream
        var upstreamTcp = new TcpClient();
        var connectPipeline = connectPipelineProvider.GetPipeline(host);
        await connectPipeline.ExecuteAsync(async token =>
        {
            await upstreamTcp.ConnectAsync(host, port, token);
            return upstreamTcp;
        }, ct);

        using (upstreamTcp)
        {
            var upstreamStream = upstreamTcp.GetStream();
            await upstreamStream.WriteAsync(headerBytes, ct);

            // Relay remaining client bytes then response
            await InterceptHttpStreamAsync(clientStream, upstreamStream, host, port, "http",
                string.Empty, string.Empty, clientIp, settings, scope, ct);
        }
    }

    // ── HTTP parse + record ─────────────────────────────────────────────────

    private async Task InterceptHttpStreamAsync(
        Stream clientStream, Stream upstreamStream,
        string host, int port, string scheme,
        string tlsVersion, string tlsCipher,
        string clientIp, ProxySettings settings,
        IServiceScope scope,
        CancellationToken ct)
    {
        var devices = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
        var captures = scope.ServiceProvider.GetRequiredService<ICaptureRepository>();

        while (!ct.IsCancellationRequested)
        {
            // Read request from client
            var (reqLine, reqHeaders, reqBody) = await ReadHttpMessageAsync(clientStream, settings.MaxBodySizeKb, ct);
            if (reqLine is null) break;

            var started = DateTimeOffset.UtcNow;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Forward to upstream
            await WriteHttpMessageAsync(upstreamStream, reqLine, reqHeaders, reqBody, ct);

            // Read response from upstream
            var (statusLine, respHeaders, respBody) = await ReadHttpMessageAsync(upstreamStream, settings.MaxBodySizeKb, ct);

            sw.Stop();

            // Forward response to client
            if (statusLine is not null)
                await WriteHttpMessageAsync(clientStream, statusLine, respHeaders, respBody, ct);

            // Parse and record
            ParseRequestLine(reqLine, out var method, out var path, out var query);
            ParseStatusLine(statusLine, out var statusCode, out var statusMsg);

            var device = await GetOrRegisterDeviceAsync(devices, clientIp, ct);
            var capture = new CapturedRequest
            {
                DeviceId = device?.Id,
                Method = method,
                Scheme = scheme,
                Host = host,
                Port = port,
                Path = path,
                Query = query,
                RequestHeaders = reqHeaders,
                RequestBody = settings.CaptureRequestBodies ? reqBody : string.Empty,
                RequestBodySize = Encoding.UTF8.GetByteCount(reqBody),
                StatusCode = statusCode,
                StatusMessage = statusMsg,
                ResponseHeaders = respHeaders,
                ResponseBody = settings.CaptureResponseBodies ? respBody : string.Empty,
                ResponseBodySize = Encoding.UTF8.GetByteCount(respBody),
                IsTls = scheme == "https",
                TlsVersion = tlsVersion,
                TlsCipherSuite = tlsCipher,
                Protocol = scheme == "https" ? InterceptionProtocol.Https : InterceptionProtocol.Http,
                Timestamp = started,
                DurationMs = sw.ElapsedMilliseconds,
                ClientIp = clientIp
            };

            await captures.AddAsync(capture, ct);
            await publisher.PublishAsync(capture, ct);

            // HTTP/1.0 or Connection: close — stop after one exchange
            if (respHeaders.Contains("Connection: close", StringComparison.OrdinalIgnoreCase) ||
                reqLine.EndsWith("HTTP/1.0"))
                break;
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static async Task TrySend503Async(TcpClient client)
    {
        try
        {
            var response = Encoding.ASCII.GetBytes(
                "HTTP/1.1 503 Service Unavailable\r\n" +
                "Content-Length: 0\r\n" +
                "Connection: close\r\n\r\n");
            await client.GetStream().WriteAsync(response);
        }
        catch { /* best-effort */ }
    }

    private static async Task<Device?> GetOrRegisterDeviceAsync(
        IDeviceRepository devices, string ip, CancellationToken ct)
    {
        if (ip is "unknown" or "127.0.0.1" or "::1") return null;
        var device = new Device { IpAddress = ip };
        return await devices.UpsertByIpAsync(device, ct);
    }

    private static async Task<(string? firstLine, string headers, string body)> ReadHttpMessageAsync(
        Stream stream, int maxBodyKb, CancellationToken ct)
    {
        string? firstLine = null;
        var headerLines = new List<string>();
        int contentLength = 0;
        bool chunked = false;

        // Read headers
        while (true)
        {
            var line = await ReadLineAsync(stream, ct);
            if (line is null) return (null, "", "");
            if (firstLine is null) { firstLine = line; continue; }
            if (string.IsNullOrEmpty(line)) break;
            headerLines.Add(line);
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                int.TryParse(line[15..].Trim(), out contentLength);
            if (line.Contains("chunked", StringComparison.OrdinalIgnoreCase))
                chunked = true;
        }

        var headers = string.Join("\r\n", headerLines);
        var body = string.Empty;

        if (contentLength > 0)
        {
            var maxBytes = maxBodyKb * 1024;
            var readLen = Math.Min(contentLength, maxBytes);
            var buf = new byte[readLen];
            await stream.ReadExactlyAsync(buf, ct);
            body = Encoding.UTF8.GetString(buf);
            // Drain remainder if we truncated
            if (contentLength > maxBytes)
            {
                var drain = new byte[contentLength - maxBytes];
                await stream.ReadExactlyAsync(drain, ct);
            }
        }
        else if (chunked)
        {
            body = await ReadChunkedBodyAsync(stream, maxBodyKb * 1024, ct);
        }

        return (firstLine, headers, body);
    }

    private static async Task<string> ReadChunkedBodyAsync(Stream stream, int maxBytes, CancellationToken ct)
    {
        var sb = new StringBuilder();
        while (true)
        {
            var sizeLine = await ReadLineAsync(stream, ct);
            if (sizeLine is null) break;
            var chunkSize = Convert.ToInt32(sizeLine.Trim().Split(';')[0], 16);
            if (chunkSize == 0)
            {
                await ReadLineAsync(stream, ct); // trailing CRLF
                break;
            }
            var buf = new byte[chunkSize];
            await stream.ReadExactlyAsync(buf, ct);
            await ReadLineAsync(stream, ct); // CRLF after chunk
            if (sb.Length < maxBytes)
                sb.Append(Encoding.UTF8.GetString(buf));
        }
        return sb.ToString();
    }

    private static async Task WriteHttpMessageAsync(
        Stream stream, string firstLine, string headers, string body, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine(firstLine);
        if (!string.IsNullOrEmpty(headers)) sb.AppendLine(headers);
        sb.AppendLine();
        if (!string.IsNullOrEmpty(body)) sb.Append(body);
        await stream.WriteAsync(Encoding.UTF8.GetBytes(sb.ToString()), ct);
    }

    private static async Task<string?> ReadLineAsync(Stream stream, CancellationToken ct)
    {
        var bytes = new List<byte>(256);
        var buf = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(buf.AsMemory(0, 1), ct);
            if (read == 0) return bytes.Count == 0 ? null : Encoding.ASCII.GetString([.. bytes]).TrimEnd('\r');
            if (buf[0] == '\n') return Encoding.ASCII.GetString([.. bytes]).TrimEnd('\r');
            bytes.Add(buf[0]);
        }
    }

    private static void ParseRequestLine(string line, out string method, out string path, out string query)
    {
        var parts = line.Split(' ');
        method = parts.Length > 0 ? parts[0] : "GET";
        var rawPath = parts.Length > 1 ? parts[1] : "/";
        var qi = rawPath.IndexOf('?');
        path = qi < 0 ? rawPath : rawPath[..qi];
        query = qi < 0 ? string.Empty : rawPath[(qi + 1)..];
    }

    private static void ParseStatusLine(string? line, out int code, out string message)
    {
        if (line is null) { code = 0; message = string.Empty; return; }
        var parts = line.Split(' ', 3);
        code = parts.Length > 1 && int.TryParse(parts[1], out var c) ? c : 0;
        message = parts.Length > 2 ? parts[2] : string.Empty;
    }

    private static X509Certificate2 BuildX509(CertificateEntry entry)
    {
        return X509Certificate2.CreateFromPem(entry.CertificatePem, entry.PrivateKeyPem);
    }
}
