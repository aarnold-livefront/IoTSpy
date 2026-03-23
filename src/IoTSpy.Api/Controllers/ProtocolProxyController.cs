using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTSpy.Api.Controllers;

[ApiController]
[Route("api/protocol-proxy")]
[Authorize]
public class ProtocolProxyController(
    IMqttBrokerProxy mqttProxy,
    ICoapProxy coapProxy) : ControllerBase
{
    // ── MQTT Broker Proxy ────────────────────────────────────────────────────

    [HttpPost("mqtt/start")]
    public async Task<IActionResult> StartMqttProxy([FromBody] MqttBrokerSettings settings, CancellationToken ct)
    {
        if (mqttProxy.IsRunning)
            return Conflict(new { error = "MQTT broker proxy is already running" });

        await mqttProxy.StartAsync(settings, ct);
        return Ok(new { status = "started", port = settings.ListenPort, upstream = $"{settings.UpstreamHost}:{settings.UpstreamPort}" });
    }

    [HttpPost("mqtt/stop")]
    public async Task<IActionResult> StopMqttProxy(CancellationToken ct)
    {
        if (!mqttProxy.IsRunning)
            return Conflict(new { error = "MQTT broker proxy is not running" });

        await mqttProxy.StopAsync(ct);
        return Ok(new { status = "stopped" });
    }

    [HttpGet("mqtt/status")]
    public IActionResult GetMqttProxyStatus()
    {
        return Ok(new
        {
            isRunning = mqttProxy.IsRunning,
            activeConnections = mqttProxy.ActiveConnections
        });
    }

    // ── CoAP Proxy ───────────────────────────────────────────────────────────

    [HttpPost("coap/start")]
    public async Task<IActionResult> StartCoapProxy([FromBody] CoapProxySettings settings, CancellationToken ct)
    {
        if (coapProxy.IsRunning)
            return Conflict(new { error = "CoAP proxy is already running" });

        await coapProxy.StartAsync(settings, ct);
        return Ok(new { status = "started", port = settings.ListenPort, upstream = $"{settings.UpstreamHost}:{settings.UpstreamPort}" });
    }

    [HttpPost("coap/stop")]
    public async Task<IActionResult> StopCoapProxy(CancellationToken ct)
    {
        if (!coapProxy.IsRunning)
            return Conflict(new { error = "CoAP proxy is not running" });

        await coapProxy.StopAsync(ct);
        return Ok(new { status = "stopped" });
    }

    [HttpGet("coap/status")]
    public IActionResult GetCoapProxyStatus()
    {
        return Ok(new
        {
            isRunning = coapProxy.IsRunning,
            messagesProxied = coapProxy.MessagesProxied
        });
    }
}
