using System.Collections.Concurrent;
using System.Text;
using IoTSpy.Core.Enums;

namespace IoTSpy.Protocols.WebSocket;

/// <summary>
/// Stateful reassembler that reconstructs fragmented WebSocket messages from a stream of
/// decoded frames, per RFC 6455 §5.4. A fragmented message is an initial Text or Binary
/// frame with FIN=0, followed by zero or more Continuation frames, terminated by a
/// Continuation frame with FIN=1. Control frames (Close/Ping/Pong) are never fragmented and
/// may legally arrive interleaved between the fragments of a data message — they pass through
/// independently and do not disturb in-flight fragmentation state.
///
/// Tracks state per connection (keyed by an opaque caller-supplied connection identifier, e.g.
/// "{clientIp}:{clientPort}-{serverIp}:{serverPort}") so multiple concurrent WebSocket
/// connections can be reassembled without interference. Thread-safe; intended as a singleton
/// service, mirroring <see cref="Mqtt.MqttSessionAnalyzer"/>.
/// </summary>
public sealed class WebSocketMessageReassembler
{
    private sealed class PendingMessage
    {
        public required WebSocketOpcode Opcode { get; init; }
        public required MemoryStream Buffer { get; init; }
        public int FrameCount;
        public DateTimeOffset StartedAt { get; init; }
    }

    private readonly ConcurrentDictionary<string, PendingMessage> _pending = new();

    /// <summary>
    /// Feeds a decoded frame belonging to <paramref name="connectionKey"/>. Returns the
    /// reconstructed logical message once a complete (possibly multi-frame) sequence has been
    /// observed; returns null while a fragmented message is still in progress.
    /// </summary>
    public WebSocketReconstructedMessage? Record(string connectionKey, WebSocketDecodedFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (frame.IsControl)
        {
            // RFC 6455 §5.4: control frames are never fragmented (FIN is always 1 for valid
            // traffic) and do not interact with any in-flight data-frame reassembly.
            return frame.Fin ? Build(frame.Opcode, frame.PayloadBytes ?? [], frameCount: 1) : null;
        }

        switch (frame.Opcode)
        {
            case WebSocketOpcode.Text:
            case WebSocketOpcode.Binary:
                if (frame.Fin)
                {
                    // Unfragmented message — nothing to buffer.
                    _pending.TryRemove(connectionKey, out _);
                    return Build(frame.Opcode, frame.PayloadBytes ?? [], frameCount: 1);
                }

                // Opens a new fragmented sequence. A start frame while one is already pending
                // for this connection replaces it (protocol violation upstream; last write wins).
                var pending = new PendingMessage
                {
                    Opcode = frame.Opcode,
                    Buffer = new MemoryStream(),
                    StartedAt = DateTimeOffset.UtcNow,
                };
                AppendFrame(pending, frame);
                _pending[connectionKey] = pending;
                return null;

            case WebSocketOpcode.Continuation:
                if (!_pending.TryGetValue(connectionKey, out var state))
                    return null; // continuation with no matching start frame — drop (out-of-order/garbage)

                AppendFrame(state, frame);

                if (!frame.Fin)
                    return null;

                _pending.TryRemove(connectionKey, out _);
                return Build(state.Opcode, state.Buffer.ToArray(), state.FrameCount);

            default:
                return null;
        }
    }

    /// <summary>Discards any in-flight fragmented message for a connection (e.g. on socket close/reset).</summary>
    public void Clear(string connectionKey) => _pending.TryRemove(connectionKey, out _);

    /// <summary>Number of connections with a fragmented message currently in progress.</summary>
    public int PendingCount => _pending.Count;

    /// <summary>Resets all tracked reassembly state.</summary>
    public void Reset() => _pending.Clear();

    private static void AppendFrame(PendingMessage pending, WebSocketDecodedFrame frame)
    {
        var payload = frame.PayloadBytes;
        if (payload is { Length: > 0 })
            pending.Buffer.Write(payload, 0, payload.Length);
        pending.FrameCount++;
    }

    private static WebSocketReconstructedMessage Build(WebSocketOpcode opcode, byte[] payload, int frameCount)
    {
        string? text = null;
        if (opcode is WebSocketOpcode.Text or WebSocketOpcode.Close)
        {
            try { text = Encoding.UTF8.GetString(payload); }
            catch { /* binary data in a text-typed frame */ }
        }

        return new WebSocketReconstructedMessage
        {
            Opcode = opcode,
            Payload = payload,
            Text = text,
            FrameCount = frameCount,
        };
    }
}
