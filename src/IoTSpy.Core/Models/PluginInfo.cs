namespace IoTSpy.Core.Models;

public sealed class PluginInfo
{
    public string Protocol { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string AssemblyPath { get; set; } = "";
    public bool IsLoaded { get; set; }
    public string? LoadError { get; set; }
}
