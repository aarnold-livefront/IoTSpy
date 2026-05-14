namespace IoTSpy.Core.Models;

public class ScanScope
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Cidr { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Guid CreatedByUserId { get; set; }
    public string CreatedByUsername { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
