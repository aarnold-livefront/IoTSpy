using IoTSpy.Core.Enums;
using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

/// <summary>
/// Applies manipulation rules and breakpoint scripts to HTTP messages flowing through the proxy.
/// Called by the proxy interception pipeline.
/// </summary>
public interface IManipulationService
{
    /// <summary>
    /// Apply all enabled rules and scripts to the given message at the specified phase.
    /// Returns true if any modification was made.
    /// </summary>
    Task<bool> ApplyAsync(HttpMessage message, ManipulationPhase phase, CancellationToken ct = default);

    /// <summary>
    /// Replay a captured request (optionally modified) and return the session with the response.
    /// </summary>
    Task<ReplaySession> ReplayAsync(ReplaySession session, CancellationToken ct = default);

    /// <summary>
    /// Start a fuzzer job against a captured request.
    /// </summary>
    Task<FuzzerJob> StartFuzzerAsync(FuzzerJob job, CancellationToken ct = default);

    /// <summary>
    /// Cancel a running fuzzer job.
    /// </summary>
    Task CancelFuzzerAsync(Guid jobId);

    /// <summary>
    /// Check if a fuzzer job is running.
    /// </summary>
    bool IsFuzzerRunning(Guid jobId);
}
