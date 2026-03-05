namespace IoTSpy.Manipulation.AiMock;

/// <summary>
/// Abstraction over AI completion providers (Claude, OpenAI, Ollama).
/// </summary>
public interface IAiProvider
{
    /// <summary>
    /// Send a system+user prompt pair and return the assistant's text response.
    /// </summary>
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}
