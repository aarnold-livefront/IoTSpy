using System.Net;
using System.Net.Sockets;
using System.Text;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using IoTSpy.Protocols.Coap;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Proxy.Interception;

/// <summary>
/// CoAP forward proxy. Listens on a UDP port, forwards CoAP requests to the upstream
/// server, and captures all messages for inspection.
/// </summary>
public class CoapProxy(
    ICapturePublisher publisher,
    IServiceScopeFactory scopeFactory,
    ILogger<CoapProxy> logger) : ICoapProxy
{
    private UdpClient? _listener;
    private CancellationTokenSource? _cts;
    private CoapProxySettings? _settings;
    private long _messagesProxied;
    private readonly CoapDecoder _decoder = new();

    public bool IsRunning => _listener is not null;
    public long MessagesProxied => _messagesProxied;

    public async Task StartAsync(CoapProxySettings settings, CancellationToken ct = default)
    {
        if (IsRunning) return;
        if (string.IsNullOrEmpty(settings.UpstreamHost))
            throw new ArgumentException("UpstreamHost is required for CoAP proxy");

        _settings = settings;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var ep = new IPEndPoint(
            settings.ListenAddress is "0.0.0.0" or "*" ? IPAddress.Any : IPAddress.Parse(settings.ListenAddress),
            settings.ListenPort);
        _listener = new UdpClient(ep);
        logger.LogInformation("CoAP proxy listening on {Addr}:{Port} → {Upstream}:{UpstreamPort}",
            settings.ListenAddress, settings.ListenPort, settings.UpstreamHost, settings.UpstreamPort);
        _ = ReceiveLoopAsync(_cts.Token);
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        _cts?.Cancel();
        _listener?.Close();
        _listener = null;
        logger.LogInformation("CoAP proxy stopped (proxied {Count} messages)", _messagesProxied);
        return Task.CompletedTask;
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var upstreamEndpoint = new IPEndPoint(
            (await Dns.GetHostAddressesAsync(_settings!.UpstreamHost!, ct))[0],
            _settings.UpstreamPort);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _listener!.ReceiveAsync(ct);
                _ = HandleMessageAsync(result.Buffer, result.RemoteEndPoint, upstreamEndpoint, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "CoAP proxy receive error");
            }
        }
    }

    private async Task HandleMessageAsync(byte[] data, IPEndPoint clientEndpoint, IPEndPoint upstreamEndpoint, CancellationToken ct)
    {
        try
        {
            Interlocked.Increment(ref _messagesProxied);

            // Decode the CoAP message
            CoapMessage? coapMsg = null;
            if (_decoder.CanDecode(data))
            {
                var decoded = await _decoder.DecodeAsync(data, ct);
                coapMsg = decoded.Count > 0 ? decoded[0] : null;
            }

            // Forward to upstream
            using var upstreamClient = new UdpClient();
            await upstreamClient.SendAsync(data, upstreamEndpoint, ct);

            // Wait for response (with timeout)
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

            byte[]? responseData = null;
            CoapMessage? responseCoapMsg = null;
            try
            {
                var response = await upstreamClient.ReceiveAsync(timeoutCts.Token);
                responseData = response.Buffer;

                if (_decoder.CanDecode(responseData))
                {
                    var decoded = await _decoder.DecodeAsync(responseData, ct);
                    responseCoapMsg = decoded.Count > 0 ? decoded[0] : null;
                }

                // Forward response back to client
                await _listener!.SendAsync(responseData, clientEndpoint, ct);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                logger.LogDebug("CoAP upstream timeout for {Client}", clientEndpoint);
            }

            // Record capture
            using var scope = scopeFactory.CreateScope();
            var captures = scope.ServiceProvider.GetRequiredService<ICaptureRepository>();
            var devices = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();

            var clientIp = clientEndpoint.Address.ToString();
            var device = await GetOrRegisterDeviceAsync(devices, clientIp, ct);

            var capture = new CapturedRequest
            {
                DeviceId = device?.Id,
                Method = coapMsg?.CodeName ?? "CoAP",
                Scheme = "coap",
                Host = _settings!.UpstreamHost!,
                Port = _settings.UpstreamPort,
                Path = coapMsg != null ? $"/{coapMsg.UriPath}" : "/",
                Query = coapMsg?.UriQuery ?? string.Empty,
                RequestHeaders = coapMsg != null
                    ? $"Type: {coapMsg.Type}\r\nCode: {coapMsg.CodeString}\r\nMessageId: {coapMsg.MessageId}"
                    : string.Empty,
                RequestBody = _settings.LogPayloads && coapMsg?.PayloadString != null
                    ? coapMsg.PayloadString
                    : string.Empty,
                RequestBodySize = data.Length,
                StatusCode = responseCoapMsg != null ? responseCoapMsg.Code : 0,
                StatusMessage = responseCoapMsg?.CodeName ?? string.Empty,
                ResponseHeaders = responseCoapMsg != null
                    ? $"Type: {responseCoapMsg.Type}\r\nCode: {responseCoapMsg.CodeString}\r\nMessageId: {responseCoapMsg.MessageId}"
                    : string.Empty,
                ResponseBody = _settings.LogPayloads && responseCoapMsg?.PayloadString != null
                    ? responseCoapMsg.PayloadString
                    : string.Empty,
                ResponseBodySize = responseData?.Length ?? 0,
                Protocol = InterceptionProtocol.CoAP,
                ClientIp = clientIp,
                DurationMs = 0
            };

            await captures.AddAsync(capture, ct);
            await publisher.PublishAsync(capture, ct);

            logger.LogDebug("CoAP {Method} /{Path} from {Client} → {StatusCode}",
                coapMsg?.CodeName, coapMsg?.UriPath, clientIp, responseCoapMsg?.CodeString);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogDebug(ex, "CoAP proxy handler error");
        }
    }

    private static async Task<Device?> GetOrRegisterDeviceAsync(
        IDeviceRepository devices, string ip, CancellationToken ct)
    {
        if (ip is "unknown" or "127.0.0.1" or "::1") return null;
        var device = new Device { IpAddress = ip };
        return await devices.UpsertByIpAsync(device, ct);
    }
}
