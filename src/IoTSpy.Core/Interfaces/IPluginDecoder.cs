using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

/// <summary>
/// A protocol decoder loaded from an external plugin assembly.
/// Implement this interface in a standalone DLL and drop it in the plugins/ directory.
/// </summary>
public interface IPluginDecoder
{
    string Protocol { get; }
    string Name { get; }
    string Version { get; }

    PluginDecodeResult Decode(ReadOnlySpan<byte> payload, string? contentType = null);
}
