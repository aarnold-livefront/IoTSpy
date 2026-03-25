using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Manipulation.ApiSpec;

/// <summary>
/// Integrates API spec mocking into the proxy pipeline. Matches incoming requests
/// against active API specs and applies content replacement rules. Supports
/// passthrough-first mode where real responses are observed before mocking begins.
/// </summary>
public sealed class ApiSpecMockService(
    ContentReplacer contentReplacer,
    IServiceScopeFactory scopeFactory,
    ILogger<ApiSpecMockService> logger) : IApiSpecService
{
    private readonly ConcurrentDictionary<string, ObservedResponse> _observationCache = new();
    private readonly ConcurrentDictionary<string, ApiSpecDocument> _specCache = new();
    private Timer? _flushTimer;
    private volatile bool _flushPending;

    public async Task<ApiSpecDocument> GenerateFromTrafficAsync(ApiSpecGenerationRequest request, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var specRepo = scope.ServiceProvider.GetRequiredService<IApiSpecRepository>();
        var generator = scope.ServiceProvider.GetRequiredService<ApiSpecGenerator>();

        var openApiJson = await generator.GenerateAsync(request, ct);

        var doc = new ApiSpecDocument
        {
            Name = request.Name ?? $"Spec for {request.Host}",
            Host = request.Host,
            OpenApiJson = openApiJson,
            UseLlmAnalysis = request.UseLlmAnalysis,
            Status = ApiSpecStatus.Draft
        };

        await specRepo.CreateAsync(doc, ct);

        // Optionally refine with LLM
        if (request.UseLlmAnalysis)
        {
            try
            {
                doc = await RefineWithLlmInternalAsync(doc, scope, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "LLM refinement failed for spec {SpecId}, continuing with raw spec", doc.Id);
            }
        }

        logger.LogInformation("Generated API spec {SpecId} for {Host}", doc.Id, request.Host);
        return doc;
    }

    public async Task<ApiSpecDocument> ImportAsync(string openApiJson, string? name = null, CancellationToken ct = default)
    {
        // Validate it's valid JSON
        JsonNode.Parse(openApiJson); // throws if invalid

        // Try to extract host from servers array
        var host = "unknown";
        try
        {
            var root = JsonNode.Parse(openApiJson)?.AsObject();
            var servers = root?["servers"]?.AsArray();
            if (servers?.Count > 0)
            {
                var url = servers[0]?["url"]?.GetValue<string>();
                if (url is not null && Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    host = uri.Host;
            }
        }
        catch { /* use "unknown" */ }

        using var scope = scopeFactory.CreateScope();
        var specRepo = scope.ServiceProvider.GetRequiredService<IApiSpecRepository>();

        var doc = new ApiSpecDocument
        {
            Name = name ?? $"Imported spec for {host}",
            Host = host,
            OpenApiJson = openApiJson,
            Status = ApiSpecStatus.Draft
        };

        await specRepo.CreateAsync(doc, ct);
        logger.LogInformation("Imported API spec {SpecId} for {Host}", doc.Id, host);
        return doc;
    }

    public async Task<string> ExportAsync(Guid specId, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var specRepo = scope.ServiceProvider.GetRequiredService<IApiSpecRepository>();

        var doc = await specRepo.GetByIdAsync(specId, ct)
            ?? throw new KeyNotFoundException($"API spec {specId} not found");

        return doc.OpenApiJson;
    }

    public async Task<ApiSpecDocument> RefineWithLlmAsync(Guid specId, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var specRepo = scope.ServiceProvider.GetRequiredService<IApiSpecRepository>();

        var doc = await specRepo.GetByIdAsync(specId, ct)
            ?? throw new KeyNotFoundException($"API spec {specId} not found");

        return await RefineWithLlmInternalAsync(doc, scope, ct);
    }

    private async Task<ApiSpecDocument> RefineWithLlmInternalAsync(ApiSpecDocument doc, IServiceScope scope, CancellationToken ct)
    {
        var enhancer = scope.ServiceProvider.GetService<ApiSpecLlmEnhancer>();
        if (enhancer is null)
        {
            logger.LogWarning("LLM enhancer not available (no AI provider configured)");
            return doc;
        }

        var specRepo = scope.ServiceProvider.GetRequiredService<IApiSpecRepository>();
        var refined = await enhancer.EnhanceAsync(doc.OpenApiJson, doc.Host, ct);
        doc.OpenApiJson = refined;
        doc.UseLlmAnalysis = true;
        await specRepo.UpdateAsync(doc, ct);

        logger.LogInformation("Refined API spec {SpecId} with LLM", doc.Id);
        return doc;
    }

    /// <summary>
    /// Called from the manipulation pipeline during response phase.
    /// Checks for active specs and applies content replacement rules.
    /// </summary>
    public async Task<bool> ApplyMockAsync(HttpMessage message, ManipulationPhase phase, CancellationToken ct = default)
    {
        if (phase != ManipulationPhase.Response) return false;

        var spec = await GetActiveSpecAsync(message.Host, ct);
        if (spec is null) return false;

        // Passthrough-first: observe and cache the real response
        if (spec.PassthroughFirst)
        {
            var cacheKey = $"{message.Method}:{ApiSpecGenerator.NormalizePath(message.Path)}";
            if (!_observationCache.ContainsKey($"{spec.Host}:{cacheKey}"))
            {
                _observationCache[$"{spec.Host}:{cacheKey}"] = new ObservedResponse
                {
                    StatusCode = message.StatusCode,
                    Headers = message.ResponseHeaders,
                    Body = message.ResponseBody,
                    ContentType = ExtractContentType(message.ResponseHeaders),
                    ObservedAt = DateTimeOffset.UtcNow
                };

                ScheduleFlush(spec);
                logger.LogDebug("Observed response for {Host} {CacheKey}", spec.Host, cacheKey);

                // Don't modify on first observation — let real response through
                // But still apply replacement rules if any are configured
            }
        }

        // Apply content replacement rules
        var enabledRules = spec.ReplacementRules
            .Where(r => r.Enabled)
            .OrderBy(r => r.Priority)
            .ToList();

        if (enabledRules.Count == 0) return false;

        return contentReplacer.Apply(message, enabledRules);
    }

    private async Task<ApiSpecDocument?> GetActiveSpecAsync(string host, CancellationToken ct)
    {
        if (_specCache.TryGetValue(host, out var cached))
            return cached;

        using var scope = scopeFactory.CreateScope();
        var specRepo = scope.ServiceProvider.GetRequiredService<IApiSpecRepository>();
        var spec = await specRepo.GetActiveForHostAsync(host, ct);

        if (spec is not null)
            _specCache[host] = spec;

        return spec;
    }

    /// <summary>
    /// Invalidate the spec cache for a host (call after spec updates).
    /// </summary>
    public void InvalidateSpecCache(string? host = null)
    {
        if (host is not null)
            _specCache.TryRemove(host, out _);
        else
            _specCache.Clear();
    }

    private void ScheduleFlush(ApiSpecDocument spec)
    {
        if (_flushPending) return;
        _flushPending = true;

        _flushTimer?.Dispose();
        _flushTimer = new Timer(async _ =>
        {
            try
            {
                await FlushObservationsAsync(spec);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to flush observations for {Host}", spec.Host);
            }
            finally
            {
                _flushPending = false;
            }
        }, null, TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
    }

    private async Task FlushObservationsAsync(ApiSpecDocument spec)
    {
        var prefix = $"{spec.Host}:";
        var observations = _observationCache
            .Where(kv => kv.Key.StartsWith(prefix))
            .ToList();

        if (observations.Count == 0) return;

        try
        {
            var root = JsonNode.Parse(spec.OpenApiJson)?.AsObject();
            if (root is null) return;

            var paths = root["paths"]?.AsObject();
            if (paths is null) return;

            foreach (var (key, obs) in observations)
            {
                var endpointKey = key[prefix.Length..]; // "GET:/api/users/{id}"
                var parts = endpointKey.Split(':', 2);
                if (parts.Length != 2) continue;

                var method = parts[0].ToLowerInvariant();
                var path = parts[1];

                if (paths[path] is not JsonObject pathItem) continue;
                if (pathItem[method] is not JsonObject operation) continue;

                // Add example to responses
                var responses = operation["responses"]?.AsObject();
                var statusKey = obs.StatusCode.ToString();
                if (responses?[statusKey] is JsonObject response)
                {
                    if (!string.IsNullOrWhiteSpace(obs.Body) && obs.Body.Length < 5000)
                    {
                        try
                        {
                            var exampleNode = JsonNode.Parse(obs.Body);
                            var content = response["content"]?.AsObject();
                            if (content is not null)
                            {
                                var firstMedia = content.FirstOrDefault();
                                if (firstMedia.Value is JsonObject mediaType)
                                    mediaType["example"] = exampleNode;
                            }
                        }
                        catch { /* not JSON, skip */ }
                    }
                }
            }

            using var scope = scopeFactory.CreateScope();
            var specRepo = scope.ServiceProvider.GetRequiredService<IApiSpecRepository>();
            spec.OpenApiJson = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            await specRepo.UpdateAsync(spec);

            logger.LogInformation("Flushed {Count} observations to spec {SpecId}", observations.Count, spec.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error flushing observations to spec");
        }
    }

    private static string ExtractContentType(string headers)
    {
        if (string.IsNullOrWhiteSpace(headers)) return string.Empty;
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(headers);
            if (dict is not null)
            {
                foreach (var (key, value) in dict)
                {
                    if (key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                        return value.Split(';')[0].Trim();
                }
            }
        }
        catch { /* ignore */ }
        return string.Empty;
    }

    internal sealed class ObservedResponse
    {
        public int StatusCode { get; init; }
        public string Headers { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
        public string ContentType { get; init; } = string.Empty;
        public DateTimeOffset ObservedAt { get; init; }
    }
}
