using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IPluginRegistry
{
    IReadOnlyList<PluginInfo> LoadedPlugins { get; }
    IPluginDecoder? GetDecoder(string protocol);
    void Reload();
}
