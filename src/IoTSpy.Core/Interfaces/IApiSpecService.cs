using IoTSpy.Core.Enums;
using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IApiSpecService
{
    Task<ApiSpecDocument> GenerateFromTrafficAsync(ApiSpecGenerationRequest request, CancellationToken ct = default);
    Task<ApiSpecDocument> ImportAsync(string openApiJson, string? name = null, CancellationToken ct = default);
    Task<string> ExportAsync(Guid specId, CancellationToken ct = default);
    Task<ApiSpecDocument> RefineWithLlmAsync(Guid specId, CancellationToken ct = default);
    Task<bool> ApplyMockAsync(HttpMessage message, ManipulationPhase phase, CancellationToken ct = default);
}
