using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

/// <summary>
/// Generates mock API responses using AI, based on captured traffic patterns for a given host.
/// </summary>
public interface IAiMockService
{
    /// <summary>
    /// Generate a mock response for the given request using AI, informed by captured traffic schemas.
    /// </summary>
    Task<AiMockResponse> GenerateResponseAsync(string host, string method, string path, string requestBody, CancellationToken ct = default);

    /// <summary>
    /// Invalidate the cached schema for a host, forcing re-analysis of captured traffic on next request.
    /// </summary>
    Task InvalidateSchemaCacheAsync(string host, CancellationToken ct = default);
}
