using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using IoTSpy.Proxy.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Timeout;

namespace IoTSpy.Proxy.Interception;

/// <summary>
/// Transparent proxy server for GatewayRedirect mode. Receives traffic redirected by
/// iptables REDIRECT rules and recovers the original destination using SO_ORIGINAL_DST.
///
/// Typical iptables setup:
///   iptables -t nat -A PREROUTING -p tcp --dport 80  -j REDIRECT --to-port 9999
///   iptables -t nat -A PREROUTING -p tcp --dport 443 -j REDIRECT --to-port 9999
/// </summary>
public class TransparentProxyServer(
    ICertificateAuthority ca,
    ICapturePublisher publisher,
    IManipulationService manipulationService,
    IOpenRtbService openRtbService,
    IServiceScopeFactory scopeFactory,
    ResiliencePipelineProvider<string> connectPipelineProvider,
    ILogger<TransparentProxyServer> logger)
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public bool IsRunning => _listener is not null;

    // Linux netfilter constants for SO_ORIGINAL_DST
    private const int SOL_IP = 0;
    private const int SO_ORIGINAL_DST = 80;

    public async Task StartAsync(int port, string listenAddress, CancellationToken ct = default)
    {
        if (IsRunning) return;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var ip = listenAddress is "0.0.0.0" or "*"
            ? IPAddress.Any
            : IPAddress.Parse(listenAddress);

        _listener = new TcpListener(ip, port);
        _listener.Start();
        logger.LogInformation("Transparent proxy listening on {Addr}:{Port}", listenAddress, port);
        _ = AcceptLoopAsync(_cts.Token);
    }

    public Task StopAsync()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _listener = null;
        logger.LogInformation("Transparent proxy stopped");
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
                logger.LogWarning(ex, "Transparent proxy accept error");
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
                var clientIp = ((IPEndPoint?)client.Client.RemoteEndPoint)?.Address.ToString() ?? "unknown";

                // Recover the original destination from the NAT table
                if (!TryGetOriginalDestination(client.Client, out var destIp, out var destPort))
                {
                    logger.LogDebug("Could not recover original destination for client {ClientIp}", clientIp);
                    return;
                }

                var host = destIp.ToString();
                logger.LogDebug("Transparent intercept: {ClientIp} → {Host}:{Port}", clientIp, host, destPort);

                using var scope = scopeFactory.CreateScope();
                var settingsRepo = scope.ServiceProvider.GetRequiredService<IProxySettingsRepository>();
                var settings = await settingsRepo.GetAsync(ct);

                // Determine if this is TLS traffic (common TLS ports: 443, 8443, 8883)
                var isTlsPort = destPort is 443 or 8443 or 8883;

                if (isTlsPort && settings.CaptureTls)
                    await HandleTlsTransparentAsync(client, host, destPort, clientIp, settings, scope, ct);
                else if (isTlsPort)
                    await HandlePassthroughAsync(client, host, destPort, ct);
                else
                    await HandlePlainTransparentAsync(client, host, destPort, clientIp, settings, scope, ct);
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
                logger.LogDebug(ex, "Transparent proxy client handler error");
            }
        }
    }

    // ── TLS MITM for transparent traffic ─────────────────────────────────────

    private async Task HandleTlsTransparentAsync(
        TcpClient client, string host, int port,
        string clientIp, ProxySettings settings, IServiceScope scope, CancellationToken ct)
    {
        var certEntry = await ca.GetOrCreateHostCertificateAsync(host, ct);
        var x509 = X509Certificate2.CreateFromPem(certEntry.CertificatePem, certEntry.PrivateKeyPem);

        var clientStream = client.GetStream();
        using var sslClient = new SslStream(clientStream, leaveInnerStreamOpen: true);
        await sslClient.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
        {
            ServerCertificate = x509,
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            ClientCertificateRequired = false
        }, ct);

        // Connect to real upstream
        var upstreamTcp = new TcpClient();
        var pipeline = connectPipelineProvider.GetPipeline(host);
        await pipeline.ExecuteAsync(async token =>
        {
            await upstreamTcp.ConnectAsync(host, port, token);
            return upstreamTcp;
        }, ct);

        using (upstreamTcp)
        {
            using var sslUpstream = new SslStream(upstreamTcp.GetStream());
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

    // ── Plain HTTP transparent interception ──────────────────────────────────

    private async Task HandlePlainTransparentAsync(
        TcpClient client, string host, int port,
        string clientIp, ProxySettings settings, IServiceScope scope, CancellationToken ct)
    {
        var clientStream = client.GetStream();

        // Connect to real upstream
        var upstreamTcp = new TcpClient();
        var pipeline = connectPipelineProvider.GetPipeline(host);
        await pipeline.ExecuteAsync(async token =>
        {
            await upstreamTcp.ConnectAsync(host, port, token);
            return upstreamTcp;
        }, ct);

        using (upstreamTcp)
        {
            var upstreamStream = upstreamTcp.GetStream();
            await InterceptHttpStreamAsync(clientStream, upstreamStream, host, port, "http",
                string.Empty, string.Empty, clientIp, settings, scope, ct);
        }
    }

    // ── Passthrough (no interception) ────────────────────────────────────────

    private async Task HandlePassthroughAsync(TcpClient client, string host, int port, CancellationToken ct)
    {
        var upstream = new TcpClient();
        var pipeline = connectPipelineProvider.GetPipeline(host);
        await pipeline.ExecuteAsync(async token =>
        {
            await upstream.ConnectAsync(host, port, token);
            return upstream;
        }, ct);

        using (upstream)
        {
            await Task.WhenAll(
                client.GetStream().CopyToAsync(upstream.GetStream(), ct),
                upstream.GetStream().CopyToAsync(client.GetStream(), ct));
        }
    }

    // ── HTTP stream interception (shared with ExplicitProxyServer) ───────────

    private async Task InterceptHttpStreamAsync(
        Stream clientStream, Stream upstreamStream,
        string host, int port, string scheme,
        string tlsVersion, string tlsCipher,
        string clientIp, ProxySettings settings,
        IServiceScope scope, CancellationToken ct)
    {
        var devices = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
        var captures = scope.ServiceProvider.GetRequiredService<ICaptureRepository>();

        while (!ct.IsCancellationRequested)
        {
            var (reqLine, reqHeaders, reqBody) = await ReadHttpMessageAsync(clientStream, settings.MaxBodySizeKb, ct);
            if (reqLine is null) break;

            var started = DateTimeOffset.UtcNow;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Parse request and apply manipulation pipeline
            ParseRequestLine(reqLine, out var method, out var path, out var query);
            var httpMsg = new HttpMessage
            {
                Method = method, Host = host, Port = port, Path = path, Query = query, Scheme = scheme,
                RequestLine = reqLine, RequestHeaders = reqHeaders, RequestBody = reqBody
            };

            // OpenRTB PII stripping (runs before general manipulation rules)
            var contentType = ExtractHeaderValue(reqHeaders, "Content-Type") ?? "";
            var modified = false;
            if (openRtbService.IsOpenRtbRequest(contentType, path, reqBody))
            {
                var rtbModified = await openRtbService.ProcessAndStripAsync(httpMsg, ManipulationPhase.Request, ct);
                if (rtbModified) modified = true;
            }

            // Phase 4: Apply request-phase manipulation rules
            var manipModified = await manipulationService.ApplyAsync(httpMsg, ManipulationPhase.Request, ct);
            if (manipModified) modified = true;

            if (modified)
            {
                reqLine = httpMsg.RequestLine;
                reqHeaders = httpMsg.RequestHeaders;
                reqBody = httpMsg.RequestBody;
            }

            if (!string.IsNullOrEmpty(reqLine))
                await WriteHttpMessageAsync(upstreamStream, reqLine, reqHeaders, reqBody, ct);

            var (statusLine, respHeaders, respBody) = await ReadHttpMessageAsync(upstreamStream, settings.MaxBodySizeKb, ct);

            // Apply response-phase manipulation pipeline
            if (statusLine is not null)
            {
                ParseStatusLine(statusLine, out var sc, out _);
                httpMsg.StatusLine = statusLine;
                httpMsg.StatusCode = sc;
                httpMsg.ResponseHeaders = respHeaders;
                httpMsg.ResponseBody = respBody;

                // OpenRTB PII stripping on response
                var respContentType = ExtractHeaderValue(respHeaders, "Content-Type") ?? "";
                if (openRtbService.IsOpenRtbRequest(respContentType, path, respBody))
                {
                    var rtbRespModified = await openRtbService.ProcessAndStripAsync(httpMsg, ManipulationPhase.Response, ct);
                    if (rtbRespModified) modified = true;
                }

                // Phase 4: Apply response-phase manipulation rules
                var respModified = await manipulationService.ApplyAsync(httpMsg, ManipulationPhase.Response, ct);
                if (respModified)
                {
                    modified = true;
                    statusLine = httpMsg.StatusLine;
                    respHeaders = httpMsg.ResponseHeaders;
                    respBody = httpMsg.ResponseBody;
                }
            }

            sw.Stop();

            if (statusLine is not null)
                await WriteHttpMessageAsync(clientStream, statusLine, respHeaders, respBody, ct);

            ParseRequestLine(reqLine ?? "", out method, out path, out query);
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
                ClientIp = clientIp,
                IsModified = modified
            };

            await captures.AddAsync(capture, ct);
            await publisher.PublishAsync(capture, ct);

            if (respHeaders.Contains("Connection: close", StringComparison.OrdinalIgnoreCase) ||
                (reqLine ?? "").EndsWith("HTTP/1.0"))
                break;
        }
    }

    // ── SO_ORIGINAL_DST (Linux netfilter) ────────────────────────────────────

    /// <summary>
    /// Recovers the original destination address from a connection redirected by
    /// iptables REDIRECT. Only works on Linux with netfilter.
    /// </summary>
    private static bool TryGetOriginalDestination(Socket socket, out IPAddress ip, out int port)
    {
        ip = IPAddress.None;
        port = 0;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return false;

        try
        {
            // struct sockaddr_in = 16 bytes: family(2) + port(2) + addr(4) + zero(8)
            var optval = new byte[16];
            socket.GetSocketOption(SocketOptionLevel.IP, (SocketOptionName)SO_ORIGINAL_DST, optval);

            // Port is bytes 2-3 in network byte order
            port = (optval[2] << 8) | optval[3];
            // IP is bytes 4-7
            ip = new IPAddress(new ReadOnlySpan<byte>(optval, 4, 4));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // ── Helpers (shared HTTP parsing logic) ──────────────────────────────────

    private static async Task TrySend503Async(TcpClient client)
    {
        try
        {
            var response = Encoding.ASCII.GetBytes(
                "HTTP/1.1 503 Service Unavailable\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            await client.GetStream().WriteAsync(response);
        }
        catch { /* best-effort */ }
    }

    private static async Task<Device?> GetOrRegisterDeviceAsync(
        IDeviceRepository devices, string ip, CancellationToken ct)
    {
        if (ip is "unknown" or "127.0.0.1" or "::1") return null;
        return await devices.UpsertByIpAsync(new Device { IpAddress = ip }, ct);
    }

    private static async Task<(string? firstLine, string headers, string body)> ReadHttpMessageAsync(
        Stream stream, int maxBodyKb, CancellationToken ct)
    {
        string? firstLine = null;
        var headerLines = new List<string>();
        int contentLength = 0;
        bool chunked = false;

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
                await ReadLineAsync(stream, ct);
                break;
            }
            var buf = new byte[chunkSize];
            await stream.ReadExactlyAsync(buf, ct);
            await ReadLineAsync(stream, ct);
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

    private static string? ExtractHeaderValue(string headers, string headerName)
    {
        foreach (var line in headers.Split("\r\n"))
        {
            if (line.StartsWith(headerName + ":", StringComparison.OrdinalIgnoreCase))
                return line[(headerName.Length + 1)..].Trim();
        }
        return null;
    }
}
