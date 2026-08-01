using IoTSpy.Core.Enums;

namespace IoTSpy.Protocols.WebSocket;

/// <summary>
/// A logical WebSocket message reassembled from one or more frames per RFC 6455 §5.4
/// (a single unfragmented data frame, or a fragmented sequence: an initial Text/Binary
/// frame with FIN=0 followed by zero or more Continuation frames, terminated by FIN=1).
/// </summary>
public sealed class WebSocketReconstructedMessage
{
    /// <summary>The opcode of the frame that started the message (Text, Binary, or a control opcode).</summary>
    public required WebSocketOpcode Opcode { get; init; }

    /// <summary>Concatenated, unmasked payload bytes across all constituent frames.</summary>
    public required byte[] Payload { get; init; }

    /// <summary>UTF-8 decoded payload when <see cref="Opcode"/> is Text (or a Close frame); otherwise null.</summary>
    public string? Text { get; init; }

    /// <summary>Number of wire frames that were concatenated to build this message.</summary>
    public required int FrameCount { get; init; }

    /// <summary>True when the message spanned more than one frame (FIN=0 initial frame + continuations).</summary>
    public bool IsFragmented => FrameCount > 1;

    public override string ToString() =>
        $"WS message opcode={Opcode} frames={FrameCount} len={Payload.Length}{(IsFragmented ? " (reassembled)" : "")}";
}
