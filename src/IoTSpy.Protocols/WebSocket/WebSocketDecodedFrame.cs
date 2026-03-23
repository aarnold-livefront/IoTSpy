using IoTSpy.Core.Enums;

namespace IoTSpy.Protocols.WebSocket;

/// <summary>
/// Represents a decoded WebSocket frame per RFC 6455.
/// </summary>
public sealed class WebSocketDecodedFrame
{
    public bool Fin { get; init; }
    public bool Rsv1 { get; init; }
    public bool Rsv2 { get; init; }
    public bool Rsv3 { get; init; }
    public WebSocketOpcode Opcode { get; init; }
    public bool Masked { get; init; }
    public long PayloadLength { get; init; }
    public byte[]? PayloadBytes { get; init; }
    public string? PayloadText { get; init; }
    public ushort? CloseCode { get; init; }
    public string? CloseReason { get; init; }
    public int TotalLength { get; init; }
    public byte[]? RawBytes { get; init; }

    public bool IsControl => Opcode is WebSocketOpcode.Close or WebSocketOpcode.Ping or WebSocketOpcode.Pong;
    public bool IsData => Opcode is WebSocketOpcode.Text or WebSocketOpcode.Binary or WebSocketOpcode.Continuation;

    public string OpcodeString => Opcode switch
    {
        WebSocketOpcode.Continuation => "Continuation",
        WebSocketOpcode.Text => "Text",
        WebSocketOpcode.Binary => "Binary",
        WebSocketOpcode.Close => "Close",
        WebSocketOpcode.Ping => "Ping",
        WebSocketOpcode.Pong => "Pong",
        _ => $"Unknown(0x{(byte)Opcode:X2})"
    };

    public override string ToString() =>
        $"WS {OpcodeString} fin={Fin} len={PayloadLength}{(Masked ? " masked" : "")}";
}
