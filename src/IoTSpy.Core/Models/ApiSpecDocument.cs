using IoTSpy.Core.Enums;

namespace IoTSpy.Core.Models;

public class ApiSpecDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string OpenApiJson { get; set; } = string.Empty;
    public ApiSpecStatus Status { get; set; } = ApiSpecStatus.Draft;
    public bool MockEnabled { get; set; }
    public bool PassthroughFirst { get; set; }
    public bool UseLlmAnalysis { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<ContentReplacementRule> ReplacementRules { get; set; } = [];
}
