using System.Text.Json;
using System.Text.Json.Nodes;
using IoTSpy.Manipulation.AiMock;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Manipulation.ApiSpec;

/// <summary>
/// Uses an AI provider to enhance a generated OpenAPI spec with better descriptions,
/// type detection, enum identification, and pattern recognition.
/// </summary>
public sealed class ApiSpecLlmEnhancer(
    IAiProvider aiProvider,
    ILogger<ApiSpecLlmEnhancer> logger)
{
    public async Task<string> EnhanceAsync(string openApiJson, string host, CancellationToken ct = default)
    {
        // Truncate spec if too large for LLM context
        var specForPrompt = openApiJson.Length > 12_000
            ? openApiJson[..12_000] + "\n... (truncated)"
            : openApiJson;

        var systemPrompt = """
            You are an API documentation expert. You will receive an auto-generated OpenAPI 3.0 spec
            that was inferred from captured HTTP traffic. Your job is to improve it:

            1. Add meaningful descriptions to endpoints and schemas
            2. Detect likely enum values from observed data patterns
            3. Improve type accuracy (e.g., detect date-time strings, UUIDs, emails, URLs)
            4. Identify field naming patterns and suggest better names if needed
            5. Mark fields that appear optional vs required based on the data
            6. Add format hints (e.g., "format": "email", "format": "uri")

            IMPORTANT: Return ONLY the improved OpenAPI JSON. No extra text, no markdown code fences.
            The output must be valid JSON that can be parsed directly.
            Keep the same structure and paths — only improve descriptions, types, and schemas.
            """;

        var userPrompt = $"Improve this OpenAPI spec for host \"{host}\":\n\n{specForPrompt}";

        logger.LogInformation("Sending spec for LLM enhancement ({Length} chars) for {Host}",
            specForPrompt.Length, host);

        var response = await aiProvider.CompleteAsync(systemPrompt, userPrompt, ct);

        // Try to parse the response as valid JSON
        try
        {
            // Strip markdown code fences if present
            var cleaned = response.Trim();
            if (cleaned.StartsWith("```"))
            {
                var firstNewline = cleaned.IndexOf('\n');
                if (firstNewline >= 0)
                    cleaned = cleaned[(firstNewline + 1)..];
                if (cleaned.EndsWith("```"))
                    cleaned = cleaned[..^3].TrimEnd();
            }

            var parsed = JsonNode.Parse(cleaned);
            if (parsed is null)
            {
                logger.LogWarning("LLM returned null JSON, keeping original spec");
                return openApiJson;
            }

            // Verify it still has the required OpenAPI structure
            var root = parsed.AsObject();
            if (!root.ContainsKey("openapi") || !root.ContainsKey("paths"))
            {
                logger.LogWarning("LLM response missing required OpenAPI fields, keeping original spec");
                return openApiJson;
            }

            logger.LogInformation("Successfully enhanced spec with LLM for {Host}", host);
            return parsed.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "LLM returned invalid JSON, keeping original spec for {Host}", host);
            return openApiJson;
        }
    }
}
