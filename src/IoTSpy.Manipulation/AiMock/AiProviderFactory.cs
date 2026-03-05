using IoTSpy.Core.Models;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Manipulation.AiMock;

/// <summary>
/// Factory that creates the appropriate <see cref="IAiProvider"/> based on configuration.
/// </summary>
public static class AiProviderFactory
{
    /// <summary>
    /// Create an AI provider from the given config, HttpClient, and logger factory.
    /// </summary>
    public static IAiProvider Create(AiProviderConfig config, HttpClient httpClient, ILoggerFactory loggerFactory)
    {
        return config.Provider.ToLowerInvariant() switch
        {
            "claude" or "anthropic" => new ClaudeProvider(
                httpClient, config, loggerFactory.CreateLogger<ClaudeProvider>()),

            "openai" or "gpt" => new OpenAiProvider(
                httpClient, config, loggerFactory.CreateLogger<OpenAiProvider>()),

            "ollama" or "local" => new OllamaProvider(
                httpClient, config, loggerFactory.CreateLogger<OllamaProvider>()),

            _ => throw new ArgumentException($"Unknown AI provider: {config.Provider}. Supported: claude, openai, ollama.")
        };
    }
}
