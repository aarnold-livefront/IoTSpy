using IoTSpy.Core.Enums;

namespace IoTSpy.Core.Models;

public class Breakpoint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public ScriptLanguage Language { get; set; }
    public string ScriptCode { get; set; } = string.Empty;

    // Optional match criteria (null = match all)
    public string? HostPattern { get; set; }
    public string? PathPattern { get; set; }
    public ManipulationPhase Phase { get; set; } = ManipulationPhase.Request;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
