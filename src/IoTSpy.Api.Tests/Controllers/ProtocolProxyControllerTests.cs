using IoTSpy.Api.Controllers;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace IoTSpy.Api.Tests.Controllers;

public class ProtocolProxyControllerTests
{
    private static ProtocolProxyController MakeController(
        IMqttBrokerProxy? mqtt = null,
        ICoapProxy? coap = null) =>
        new(
            mqtt ?? Substitute.For<IMqttBrokerProxy>(),
            coap ?? Substitute.For<ICoapProxy>());

    // ── MQTT ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartMqttProxy_WhenNotRunning_StartsAndReturnsOk()
    {
        var mqtt = Substitute.For<IMqttBrokerProxy>();
        mqtt.IsRunning.Returns(false);

        var controller = MakeController(mqtt);
        var settings = new MqttBrokerSettings { ListenPort = 1883, UpstreamHost = "broker.local", UpstreamPort = 1883 };
        var result = await controller.StartMqttProxy(settings, TestContext.Current.CancellationToken) as OkObjectResult;

        Assert.NotNull(result);
        await mqtt.Received(1).StartAsync(settings, Arg.Any<CancellationToken>());
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("\"status\":\"started\"", json);
        Assert.Contains("\"port\":1883", json);
    }

    [Fact]
    public async Task StartMqttProxy_WhenAlreadyRunning_ReturnsConflict()
    {
        var mqtt = Substitute.For<IMqttBrokerProxy>();
        mqtt.IsRunning.Returns(true);

        var controller = MakeController(mqtt);
        var settings = new MqttBrokerSettings();
        var result = await controller.StartMqttProxy(settings, TestContext.Current.CancellationToken);

        Assert.IsType<ConflictObjectResult>(result);
        await mqtt.DidNotReceive().StartAsync(Arg.Any<MqttBrokerSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StopMqttProxy_WhenRunning_StopsAndReturnsOk()
    {
        var mqtt = Substitute.For<IMqttBrokerProxy>();
        mqtt.IsRunning.Returns(true);

        var controller = MakeController(mqtt);
        var result = await controller.StopMqttProxy(TestContext.Current.CancellationToken) as OkObjectResult;

        Assert.NotNull(result);
        await mqtt.Received(1).StopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StopMqttProxy_WhenNotRunning_ReturnsConflict()
    {
        var mqtt = Substitute.For<IMqttBrokerProxy>();
        mqtt.IsRunning.Returns(false);

        var controller = MakeController(mqtt);
        var result = await controller.StopMqttProxy(TestContext.Current.CancellationToken);

        Assert.IsType<ConflictObjectResult>(result);
        await mqtt.DidNotReceive().StopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void GetMqttProxyStatus_ReflectsProxyState()
    {
        var mqtt = Substitute.For<IMqttBrokerProxy>();
        mqtt.IsRunning.Returns(true);
        mqtt.ActiveConnections.Returns(7);

        var controller = MakeController(mqtt);
        var result = controller.GetMqttProxyStatus() as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("\"isRunning\":true", json);
        Assert.Contains("\"activeConnections\":7", json);
    }

    // ── CoAP ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartCoapProxy_WhenNotRunning_StartsAndReturnsOk()
    {
        var coap = Substitute.For<ICoapProxy>();
        coap.IsRunning.Returns(false);

        var controller = MakeController(coap: coap);
        var settings = new CoapProxySettings { ListenPort = 5683, UpstreamHost = "coap.local", UpstreamPort = 5683 };
        var result = await controller.StartCoapProxy(settings, TestContext.Current.CancellationToken) as OkObjectResult;

        Assert.NotNull(result);
        await coap.Received(1).StartAsync(settings, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartCoapProxy_WhenAlreadyRunning_ReturnsConflict()
    {
        var coap = Substitute.For<ICoapProxy>();
        coap.IsRunning.Returns(true);

        var controller = MakeController(coap: coap);
        var result = await controller.StartCoapProxy(new CoapProxySettings(), TestContext.Current.CancellationToken);

        Assert.IsType<ConflictObjectResult>(result);
        await coap.DidNotReceive().StartAsync(Arg.Any<CoapProxySettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StopCoapProxy_WhenRunning_StopsAndReturnsOk()
    {
        var coap = Substitute.For<ICoapProxy>();
        coap.IsRunning.Returns(true);

        var controller = MakeController(coap: coap);
        var result = await controller.StopCoapProxy(TestContext.Current.CancellationToken) as OkObjectResult;

        Assert.NotNull(result);
        await coap.Received(1).StopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StopCoapProxy_WhenNotRunning_ReturnsConflict()
    {
        var coap = Substitute.For<ICoapProxy>();
        coap.IsRunning.Returns(false);

        var controller = MakeController(coap: coap);
        var result = await controller.StopCoapProxy(TestContext.Current.CancellationToken);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public void GetCoapProxyStatus_ReflectsProxyState()
    {
        var coap = Substitute.For<ICoapProxy>();
        coap.IsRunning.Returns(true);
        coap.MessagesProxied.Returns(42L);

        var controller = MakeController(coap: coap);
        var result = controller.GetCoapProxyStatus() as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("\"isRunning\":true", json);
        Assert.Contains("\"messagesProxied\":42", json);
    }
}
