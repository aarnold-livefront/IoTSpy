namespace IoTSpy.Core.Models;

/// <summary>
/// Represents an AI-generated mock HTTP response.
/// </summary>
public class AiMockResponse
{
    public int StatusCode { get; set; } = 200;
    public string Headers { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public long GenerationMs { get; set; }
}
