namespace IoTSpy.Core.Models;

public class ApiSpecGenerationRequest
{
    public string Host { get; set; } = string.Empty;
    public string? PathPattern { get; set; }
    public string? Method { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public bool UseLlmAnalysis { get; set; }
    public string? Name { get; set; }
}
