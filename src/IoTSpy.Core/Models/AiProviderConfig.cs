namespace IoTSpy.Core.Models;

/// <summary>
/// Configuration for the AI mock engine provider (Claude, OpenAI, or Ollama).
/// Bound from the "AiMock" configuration section.
/// </summary>
public class AiProviderConfig
{
    public const string SectionName = "AiMock";

    /// <summary>
    /// AI provider: "claude", "openai", or "ollama".
    /// </summary>
    public string Provider { get; set; } = "claude";

    /// <summary>
    /// Model identifier (e.g. "claude-sonnet-4-6", "gpt-4o", "llama3").
    /// </summary>
    public string Model { get; set; } = "claude-sonnet-4-6";

    /// <summary>
    /// API key for Claude or OpenAI providers.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Base URL for Ollama (e.g. "http://localhost:11434"). Ignored for cloud providers.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}
