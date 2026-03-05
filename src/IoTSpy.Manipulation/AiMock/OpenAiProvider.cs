using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using IoTSpy.Core.Models;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Manipulation.AiMock;

/// <summary>
/// AI provider that calls the OpenAI Chat Completions API.
/// </summary>
public sealed class OpenAiProvider(
    HttpClient httpClient,
    AiProviderConfig config,
    ILogger<OpenAiProvider> logger) : IAiProvider
{
    private const string ApiUrl = "https://api.openai.com/v1/chat/completions";

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var request = new OpenAiRequest
        {
            Model = config.Model,
            MaxTokens = 4096,
            Messages =
            [
                new OpenAiMessage { Role = "system", Content = systemPrompt },
                new OpenAiMessage { Role = "user", Content = userPrompt }
            ]
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
        httpRequest.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
        httpRequest.Content = JsonContent.Create(request, options: JsonOpts);

        logger.LogDebug("Calling OpenAI API with model {Model}", config.Model);

        var response = await httpClient.SendAsync(httpRequest, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("OpenAI API returned {StatusCode}: {Body}", (int)response.StatusCode, body);
            throw new HttpRequestException($"OpenAI API error {(int)response.StatusCode}: {body}");
        }

        var result = JsonSerializer.Deserialize<OpenAiResponse>(body, JsonOpts);
        return result?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed class OpenAiRequest
    {
        public string Model { get; set; } = string.Empty;
        public int MaxTokens { get; set; }
        public List<OpenAiMessage> Messages { get; set; } = [];
    }

    private sealed class OpenAiMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private sealed class OpenAiResponse
    {
        public List<OpenAiChoice>? Choices { get; set; }
    }

    private sealed class OpenAiChoice
    {
        public OpenAiMessage? Message { get; set; }
    }
}
