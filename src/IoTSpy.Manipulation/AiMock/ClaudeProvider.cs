using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using IoTSpy.Core.Models;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Manipulation.AiMock;

/// <summary>
/// AI provider that calls the Anthropic Messages API.
/// </summary>
public sealed class ClaudeProvider(
    HttpClient httpClient,
    AiProviderConfig config,
    ILogger<ClaudeProvider> logger) : IAiProvider
{
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var request = new ClaudeRequest
        {
            Model = config.Model,
            MaxTokens = 4096,
            System = systemPrompt,
            Messages = [new ClaudeMessage { Role = "user", Content = userPrompt }]
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
        httpRequest.Headers.Add("x-api-key", config.ApiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");
        httpRequest.Content = JsonContent.Create(request, options: JsonOpts);

        logger.LogDebug("Calling Claude API with model {Model}", config.Model);

        var response = await httpClient.SendAsync(httpRequest, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Claude API returned {StatusCode}: {Body}", (int)response.StatusCode, body);
            throw new HttpRequestException($"Claude API error {(int)response.StatusCode}: {body}");
        }

        var result = JsonSerializer.Deserialize<ClaudeResponse>(body, JsonOpts);
        return result?.Content?.FirstOrDefault()?.Text ?? string.Empty;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed class ClaudeRequest
    {
        public string Model { get; set; } = string.Empty;
        public int MaxTokens { get; set; }
        public string? System { get; set; }
        public List<ClaudeMessage> Messages { get; set; } = [];
    }

    private sealed class ClaudeMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private sealed class ClaudeResponse
    {
        public List<ClaudeContentBlock>? Content { get; set; }
    }

    private sealed class ClaudeContentBlock
    {
        public string Type { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}
