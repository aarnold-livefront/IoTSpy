using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

/// <summary>
/// Accepts captured HTTP/HTTPS requests from the proxy hot path and persists
/// them to the database in batches, dramatically reducing per-request DB I/O.
/// Implemented by <c>CaptureBatchWriter</c> in the API layer.
/// </summary>
public interface ICaptureBatchWriter
{
    /// <summary>
    /// Enqueues a capture for deferred persistence. Returns false only if the
    /// internal buffer is full (backpressure); the capture is dropped rather than
    /// blocking the proxy request thread.
    /// </summary>
    bool TryEnqueue(CapturedRequest capture);
}
