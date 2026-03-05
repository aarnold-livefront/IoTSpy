using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using IoTSpy.Core.Models;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Manipulation.AiMock;

/// <summary>
/// AI provider that calls a local Ollama instance.
/// </summary>
public sealed class OllamaProvider(
    HttpClient httpClient,
    AiProviderConfig config,
    ILogger<OllamaProvider> logger) : IAiProvider
{
    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var baseUrl = string.IsNullOrWhiteSpace(config.BaseUrl)
            ? "http://localhost:11434"
            : config.BaseUrl.TrimEnd('/');

        var request = new OllamaRequest
        {
            Model = config.Model,
            System = systemPrompt,
            Prompt = userPrompt,
            Stream = false
        };

        var url = $"{baseUrl}/api/generate";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Content = JsonContent.Create(request, options: JsonOpts);

        logger.LogDebug("Calling Ollama at {Url} with model {Model}", url, config.Model);

        var response = await httpClient.SendAsync(httpRequest, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Ollama API returned {StatusCode}: {Body}", (int)response.StatusCode, body);
            throw new HttpRequestException($"Ollama API error {(int)response.StatusCode}: {body}");
        }

        var result = JsonSerializer.Deserialize<OllamaResponse>(body, JsonOpts);
        return result?.Response ?? string.Empty;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed class OllamaRequest
    {
        public string Model { get; set; } = string.Empty;
        public string? System { get; set; }
        public string Prompt { get; set; } = string.Empty;
        public bool Stream { get; set; }
    }

    private sealed class OllamaResponse
    {
        public string Response { get; set; } = string.Empty;
    }
}
