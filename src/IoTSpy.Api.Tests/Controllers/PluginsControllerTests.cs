using IoTSpy.Api.Controllers;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace IoTSpy.Api.Tests.Controllers;

// Hand-rolled fake because IPluginDecoder.Decode takes ReadOnlySpan<byte>,
// which Castle DynamicProxy / NSubstitute cannot proxy.
internal sealed class FakePluginDecoder : IPluginDecoder
{
    public string Protocol { get; init; } = "test";
    public string Name { get; init; } = "test";
    public string Version { get; init; } = "1.0";
    public PluginDecodeResult? ResultToReturn { get; set; }
    public int CallCount { get; private set; }
    public byte[]? LastPayload { get; private set; }
    public string? LastContentType { get; private set; }

    public PluginDecodeResult Decode(ReadOnlySpan<byte> payload, string? contentType = null)
    {
        CallCount++;
        LastPayload = payload.ToArray();
        LastContentType = contentType;
        return ResultToReturn ?? new PluginDecodeResult { Success = true };
    }
}

public class PluginsControllerTests
{
    private static PluginInfo MakePlugin(string protocol = "lwm2m") => new()
    {
        Protocol = protocol,
        Name = $"{protocol}-plugin",
        Version = "1.0.0",
        AssemblyPath = $"/plugins/{protocol}.dll",
        IsLoaded = true
    };

    [Fact]
    public void GetAll_ReturnsAllLoadedPlugins()
    {
        var registry = Substitute.For<IPluginRegistry>();
        registry.LoadedPlugins.Returns(new[] { MakePlugin("lwm2m"), MakePlugin("homie") });

        var controller = new PluginsController(registry);
        var result = controller.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var plugins = Assert.IsAssignableFrom<IReadOnlyList<PluginInfo>>(ok.Value);
        Assert.Equal(2, plugins.Count);
    }

    [Fact]
    public void GetByProtocol_WhenFound_ReturnsPlugin()
    {
        var registry = Substitute.For<IPluginRegistry>();
        registry.LoadedPlugins.Returns(new[] { MakePlugin("lwm2m") });

        var controller = new PluginsController(registry);
        var result = controller.GetByProtocol("lwm2m");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var plugin = Assert.IsType<PluginInfo>(ok.Value);
        Assert.Equal("lwm2m", plugin.Protocol);
    }

    [Fact]
    public void GetByProtocol_IsCaseInsensitive()
    {
        var registry = Substitute.For<IPluginRegistry>();
        registry.LoadedPlugins.Returns(new[] { MakePlugin("LWM2M") });

        var controller = new PluginsController(registry);
        var result = controller.GetByProtocol("lwm2m");

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public void GetByProtocol_WhenMissing_ReturnsNotFound()
    {
        var registry = Substitute.For<IPluginRegistry>();
        registry.LoadedPlugins.Returns(Array.Empty<PluginInfo>());

        var controller = new PluginsController(registry);
        var result = controller.GetByProtocol("missing");

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public void Reload_CallsRegistryAndReturnsCount()
    {
        var registry = Substitute.For<IPluginRegistry>();
        registry.LoadedPlugins.Returns(new[] { MakePlugin("a"), MakePlugin("b"), MakePlugin("c") });

        var controller = new PluginsController(registry);
        var result = controller.Reload() as OkObjectResult;

        Assert.NotNull(result);
        registry.Received(1).Reload();
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("\"reloaded\":3", json);
    }

    [Fact]
    public void Reload_RequiresAdminRole_LowercaseToMatchAuthServiceClaim()
    {
        // Regression guard for the role-case bug fixed in #59:
        // AuthService emits ClaimTypes.Role as lowercase "admin"; the [Authorize(Roles = ...)]
        // attribute must use the same casing or the endpoint 403s for everyone.
        var method = typeof(PluginsController).GetMethod(nameof(PluginsController.Reload))!;
        var attrs = method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .ToList();

        Assert.Single(attrs);
        Assert.Equal("admin", attrs[0].Roles);
    }

    [Fact]
    public void Decode_WhenDecoderFound_ReturnsResult()
    {
        var expected = new PluginDecodeResult { Success = true, Protocol = "lwm2m", Summary = "decoded ok" };
        var decoder = new FakePluginDecoder { ResultToReturn = expected };

        var registry = Substitute.For<IPluginRegistry>();
        registry.GetDecoder("lwm2m").Returns(decoder);

        var controller = new PluginsController(registry);
        var request = new DecodeRequest
        {
            PayloadBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 }),
            ContentType = "application/octet-stream"
        };
        var result = controller.Decode("lwm2m", request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
        Assert.Equal(1, decoder.CallCount);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, decoder.LastPayload);
        Assert.Equal("application/octet-stream", decoder.LastContentType);
    }

    [Fact]
    public void Decode_WhenNoDecoder_ReturnsNotFound()
    {
        var registry = Substitute.For<IPluginRegistry>();
        registry.GetDecoder(Arg.Any<string>()).Returns((IPluginDecoder?)null);

        var controller = new PluginsController(registry);
        var request = new DecodeRequest { PayloadBase64 = Convert.ToBase64String(new byte[] { 1 }) };
        var result = controller.Decode("missing", request);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public void Decode_WithInvalidBase64_ReturnsBadRequest()
    {
        var decoder = new FakePluginDecoder();
        var registry = Substitute.For<IPluginRegistry>();
        registry.GetDecoder("lwm2m").Returns(decoder);

        var controller = new PluginsController(registry);
        var request = new DecodeRequest { PayloadBase64 = "not_valid_base64!!!" };
        var result = controller.Decode("lwm2m", request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, decoder.CallCount);
    }
}
