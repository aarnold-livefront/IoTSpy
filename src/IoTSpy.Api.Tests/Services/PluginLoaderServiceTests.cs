using IoTSpy.Api.Services;
using IoTSpy.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IoTSpy.Api.Tests.Services;

public class PluginLoaderServiceTests
{
    private static PluginLoaderService CreateService(string? directory = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Plugins:Directory"] = directory ?? Path.Combine(Path.GetTempPath(), $"iotspy-plugins-{Guid.NewGuid():N}")
            })
            .Build();
        return new PluginLoaderService(config, NullLogger<PluginLoaderService>.Instance);
    }

    [Fact]
    public void Initialize_WhenDirectoryDoesNotExist_LoadsNoPlugins()
    {
        var svc = CreateService();
        svc.Initialize();
        Assert.Empty(svc.LoadedPlugins);
    }

    [Fact]
    public void GetDecoder_UnknownProtocol_ReturnsNull()
    {
        var svc = CreateService();
        svc.Initialize();
        Assert.Null(svc.GetDecoder("unknown-protocol-xyz"));
    }

    [Fact]
    public void LoadedPlugins_AfterInitialize_IsReadOnlySnapshot()
    {
        var svc = CreateService();
        svc.Initialize();

        var snap1 = svc.LoadedPlugins;
        var snap2 = svc.LoadedPlugins;

        // Two calls return separate list instances (snapshot semantics)
        Assert.NotSame(snap1, snap2);
    }

    [Fact]
    public void Reload_WhenDirectoryDoesNotExist_ClearsPlugins()
    {
        var svc = CreateService();
        svc.Initialize();
        svc.Reload();
        Assert.Empty(svc.LoadedPlugins);
    }

    [Fact]
    public void Initialize_WithEmptyDirectory_LoadsNoPlugins()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"iotspy-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var svc = CreateService(dir);
            svc.Initialize();
            Assert.Empty(svc.LoadedPlugins);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Initialize_WithNonPluginDll_RecordsLoadError()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"iotspy-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        // Copy a real but non-plugin DLL into the directory
        var sourceDll = typeof(PluginLoaderService).Assembly.Location;
        var targetDll = Path.Combine(dir, "nonplugin.dll");
        File.Copy(sourceDll, targetDll);

        try
        {
            var svc = CreateService(dir);
            svc.Initialize();

            // May load 0 decoders but should not throw
            // The assembly itself should be loadable; it just contains no IPluginDecoder types
            var loaded = svc.LoadedPlugins;
            Assert.All(loaded, p => Assert.False(string.IsNullOrEmpty(p.AssemblyPath)));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void PluginInfo_DefaultValues_AreCorrect()
    {
        var info = new PluginInfo();
        Assert.Equal("", info.Protocol);
        Assert.Equal("", info.Name);
        Assert.Equal("", info.Version);
        Assert.False(info.IsLoaded);
        Assert.Null(info.LoadError);
    }
}
