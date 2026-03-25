using IoTSpy.Api.Controllers;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace IoTSpy.Api.Tests.Controllers;

public class ProxyControllerTests
{
    private static IProxyService CreateProxyService(bool isRunning = false, int port = 8888)
    {
        var svc = Substitute.For<IProxyService>();
        svc.IsRunning.Returns(isRunning);
        svc.Port.Returns(port);
        svc.GetSettings().Returns(new ProxySettings { ProxyPort = port });
        return svc;
    }

    [Fact]
    public void Status_ReturnsCurrentState()
    {
        var proxy = CreateProxyService(isRunning: true, port: 8888);
        var controller = new ProxyController(proxy);

        var result = controller.Status() as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("true", json);
        Assert.Contains("8888", json);
    }

    [Fact]
    public void Status_WhenNotRunning_ReturnsIsRunningFalse()
    {
        var proxy = CreateProxyService(isRunning: false);
        var controller = new ProxyController(proxy);

        var result = controller.Status() as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("false", json);
    }

    [Fact]
    public async Task Start_CallsProxyStartAndReturnsOk()
    {
        var proxy = CreateProxyService();
        proxy.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var controller = new ProxyController(proxy);

        var result = await controller.Start() as OkObjectResult;

        Assert.NotNull(result);
        await proxy.Received(1).StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stop_CallsProxyStopAndReturnsOk()
    {
        var proxy = CreateProxyService(isRunning: true);
        proxy.StopAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var controller = new ProxyController(proxy);

        var result = await controller.Stop() as OkObjectResult;

        Assert.NotNull(result);
        await proxy.Received(1).StopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateSettings_CallsUpdateAndReturnsSettings()
    {
        var proxy = CreateProxyService();
        proxy.UpdateSettingsAsync(Arg.Any<ProxySettings>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var controller = new ProxyController(proxy);

        var dto = new UpdateProxySettingsDto(ProxyPort: 9999);
        var result = await controller.UpdateSettings(dto, TestContext.Current.CancellationToken) as OkObjectResult;

        Assert.NotNull(result);
        await proxy.Received(1).UpdateSettingsAsync(Arg.Any<ProxySettings>(), Arg.Any<CancellationToken>());
    }
}
