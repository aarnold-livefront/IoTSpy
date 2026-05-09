using System.Text;
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

    /// <summary>
    /// Sub-protocol detected from payload content (heuristic). Null when unknown or a control frame.
    /// </summary>
    public WsSubProtocol? DetectedSubProtocol { get; init; }

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
        $"WS {OpcodeString} fin={Fin} len={PayloadLength}{(Masked ? " masked" : "")}" +
        (DetectedSubProtocol.HasValue ? $" sub={DetectedSubProtocol}" : "");
}

/// <summary>
/// Application sub-protocol detected from WebSocket frame payload content.
/// </summary>
public enum WsSubProtocol
{
    /// <summary>STOMP messaging protocol (text frames starting with a STOMP command).</summary>
    Stomp,
    /// <summary>WAMP (Web Application Messaging Protocol) — JSON array with integer type code.</summary>
    Wamp,
    /// <summary>MQTT-over-WebSocket — binary frame whose first byte is a valid MQTT fixed header.</summary>
    MqttOverWs,
}
