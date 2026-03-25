namespace IoTSpy.Core.Models;

public class DashboardLayout
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = "Default";
    public bool IsDefault { get; set; }

    /// <summary>
    /// JSON-serialized panel layout configuration.
    /// </summary>
    public string LayoutJson { get; set; } = "{}";

    /// <summary>
    /// JSON-serialized saved filter presets.
    /// </summary>
    public string FiltersJson { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
