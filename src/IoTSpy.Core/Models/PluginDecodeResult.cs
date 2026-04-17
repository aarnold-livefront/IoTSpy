namespace IoTSpy.Core.Models;

public sealed class PluginDecodeResult
{
    public bool Success { get; set; }
    public string? Protocol { get; set; }
    public string? Summary { get; set; }
    public Dictionary<string, object?> Fields { get; set; } = new();
    public string? Error { get; set; }
}
