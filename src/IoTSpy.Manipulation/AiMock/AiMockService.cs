using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Manipulation.AiMock;

/// <summary>
/// AI-powered mock response generator. Analyses captured traffic for a host to build a "schema",
/// then uses an AI provider to generate realistic mock responses for new requests.
/// </summary>
public sealed class AiMockService(
    IAiProvider provider,
    AiProviderConfig config,
    IServiceScopeFactory scopeFactory,
    ILogger<AiMockService> logger) : IAiMockService
{
    private readonly ConcurrentDictionary<string, string> _schemaCache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<AiMockResponse> GenerateResponseAsync(
        string host, string method, string path, string requestBody, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // Build or retrieve cached schema for the host
        var schema = await GetOrBuildSchemaAsync(host, ct);

        // Construct the system prompt
        var systemPrompt = BuildSystemPrompt(host, schema);

        // Construct the user prompt with the incoming request
        var userPrompt = BuildUserPrompt(method, path, requestBody);

        logger.LogDebug("Generating AI mock response for {Method} {Host}{Path}", method, host, path);

        // Call the AI provider
        var rawResponse = await provider.CompleteAsync(systemPrompt, userPrompt, ct);

        // Parse the response into structured fields
        var result = ParseAiResponse(rawResponse);
        result.Model = config.Model;
        result.GenerationMs = sw.ElapsedMilliseconds;

        logger.LogInformation("AI mock generated {StatusCode} response for {Method} {Host}{Path} in {Ms}ms",
            result.StatusCode, method, host, path, result.GenerationMs);

        return result;
    }

    public Task InvalidateSchemaCacheAsync(string host, CancellationToken ct = default)
    {
        _schemaCache.TryRemove(host, out _);
        logger.LogInformation("Invalidated AI mock schema cache for {Host}", host);
        return Task.CompletedTask;
    }

    private async Task<string> GetOrBuildSchemaAsync(string host, CancellationToken ct)
    {
        if (_schemaCache.TryGetValue(host, out var cached))
            return cached;

        var schema = await BuildSchemaFromCapturesAsync(host, ct);
        _schemaCache[host] = schema;
        return schema;
    }

    private async Task<string> BuildSchemaFromCapturesAsync(string host, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var captureRepo = scope.ServiceProvider.GetRequiredService<ICaptureRepository>();

        var filter = new CaptureFilter(HostContains: host);
        var captures = await captureRepo.GetPagedAsync(filter, page: 1, pageSize: 20, ct);

        if (captures.Count == 0)
            return "No captured traffic available for this host. Generate a plausible REST API response.";

        var sb = new StringBuilder();
        sb.AppendLine($"Observed traffic patterns for {host} ({captures.Count} samples):");
        sb.AppendLine();

        // Group by method+path to summarize endpoints
        var endpoints = captures
            .GroupBy(c => $"{c.Method} {c.Path}")
            .Take(15);

        foreach (var group in endpoints)
        {
            var sample = group.First();
            sb.AppendLine($"Endpoint: {group.Key}");
            sb.AppendLine($"  Status codes seen: {string.Join(", ", group.Select(c => c.StatusCode).Distinct())}");
            sb.AppendLine($"  Content types: {ExtractContentTypes(group)}");

            // Include a sample response body (truncated)
            if (!string.IsNullOrWhiteSpace(sample.ResponseBody))
            {
                var bodyPreview = sample.ResponseBody.Length > 500
                    ? sample.ResponseBody[..500] + "..."
                    : sample.ResponseBody;
                sb.AppendLine($"  Sample response body: {bodyPreview}");
            }

            // Include sample request body if present
            if (!string.IsNullOrWhiteSpace(sample.RequestBody))
            {
                var reqPreview = sample.RequestBody.Length > 300
                    ? sample.RequestBody[..300] + "..."
                    : sample.RequestBody;
                sb.AppendLine($"  Sample request body: {reqPreview}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string ExtractContentTypes(IEnumerable<CapturedRequest> captures)
    {
        var types = new HashSet<string>();
        foreach (var c in captures)
        {
            if (string.IsNullOrWhiteSpace(c.ResponseHeaders)) continue;
            try
            {
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(c.ResponseHeaders);
                if (headers is not null)
                {
                    foreach (var kv in headers)
                    {
                        if (kv.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                            types.Add(kv.Value);
                    }
                }
            }
            catch
            {
                // Headers may not be valid JSON — skip
            }
        }
        return types.Count > 0 ? string.Join(", ", types) : "unknown";
    }

    private static string BuildSystemPrompt(string host, string schema)
    {
        return $"""
            You are simulating the HTTP server at "{host}". Based on the captured traffic patterns below,
            generate a realistic mock response for incoming requests.

            {schema}

            IMPORTANT: Respond ONLY in the following format with no extra text:
            STATUS: <status_code>
            HEADERS:
            <header_name>: <header_value>
            ...
            BODY:
            <response_body>

            Rules:
            - Use the same data formats, field names, and patterns observed in the captured traffic.
            - Generate plausible data values (IDs, timestamps, names, etc.) that fit the patterns.
            - If no captures exist, generate a plausible REST API JSON response.
            - Always include a Content-Type header.
            - For JSON responses, produce valid JSON.
            """;
    }

    private static string BuildUserPrompt(string method, string path, string requestBody)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Generate a response for this request:");
        sb.AppendLine($"Method: {method}");
        sb.AppendLine($"Path: {path}");
        if (!string.IsNullOrWhiteSpace(requestBody))
            sb.AppendLine($"Request Body: {requestBody}");
        return sb.ToString();
    }

    private static AiMockResponse ParseAiResponse(string raw)
    {
        var response = new AiMockResponse
        {
            StatusCode = 200,
            Headers = "Content-Type: application/json",
            Body = raw
        };

        // Try to parse the structured format
        var statusIdx = raw.IndexOf("STATUS:", StringComparison.OrdinalIgnoreCase);
        var headersIdx = raw.IndexOf("HEADERS:", StringComparison.OrdinalIgnoreCase);
        var bodyIdx = raw.IndexOf("BODY:", StringComparison.OrdinalIgnoreCase);

        if (statusIdx < 0 || headersIdx < 0 || bodyIdx < 0)
            return response;

        // Parse status code
        var statusLine = raw[(statusIdx + 7)..headersIdx].Trim();
        if (int.TryParse(statusLine, out var statusCode))
            response.StatusCode = statusCode;

        // Parse headers
        var headersBlock = raw[(headersIdx + 8)..bodyIdx].Trim();
        response.Headers = headersBlock;

        // Parse body
        var bodyBlock = raw[(bodyIdx + 5)..].Trim();
        response.Body = bodyBlock;

        return response;
    }
}
