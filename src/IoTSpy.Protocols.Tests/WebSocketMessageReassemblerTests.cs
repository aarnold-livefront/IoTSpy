using System.Text;
using IoTSpy.Core.Enums;
using IoTSpy.Protocols.WebSocket;
using Xunit;

namespace IoTSpy.Protocols.Tests;

public class WebSocketMessageReassemblerTests
{
    private const string Conn = "10.0.0.1:5555-10.0.0.2:80";

    private static WebSocketDecodedFrame Frame(WebSocketOpcode opcode, bool fin, string text) =>
        new()
        {
            Opcode = opcode,
            Fin = fin,
            PayloadBytes = Encoding.UTF8.GetBytes(text),
            PayloadText = opcode == WebSocketOpcode.Text ? text : null,
        };

    private static WebSocketDecodedFrame BinaryFrame(WebSocketOpcode opcode, bool fin, byte[] payload) =>
        new()
        {
            Opcode = opcode,
            Fin = fin,
            PayloadBytes = payload,
        };

    private static WebSocketDecodedFrame ControlFrame(WebSocketOpcode opcode, bool fin = true, byte[]? payload = null) =>
        new()
        {
            Opcode = opcode,
            Fin = fin,
            PayloadBytes = payload ?? [],
        };

    // ── Unfragmented messages ──────────────────────────────────────────────────

    [Fact]
    public void Record_UnfragmentedTextFrame_ReturnsMessageImmediately()
    {
        var reassembler = new WebSocketMessageReassembler();

        var result = reassembler.Record(Conn, Frame(WebSocketOpcode.Text, fin: true, "hello"));

        Assert.NotNull(result);
        Assert.Equal("hello", result!.Text);
        Assert.Equal(1, result.FrameCount);
        Assert.False(result.IsFragmented);
        Assert.Equal(0, reassembler.PendingCount);
    }

    [Fact]
    public void Record_UnfragmentedBinaryFrame_ReturnsMessageImmediately()
    {
        var reassembler = new WebSocketMessageReassembler();
        byte[] payload = [1, 2, 3, 4];

        var result = reassembler.Record(Conn, BinaryFrame(WebSocketOpcode.Binary, fin: true, payload));

        Assert.NotNull(result);
        Assert.Equal(payload, result!.Payload);
        Assert.Null(result.Text);
        Assert.False(result.IsFragmented);
    }

    // ── Fragmented messages ─────────────────────────────────────────────────────

    [Fact]
    public void Record_FragmentedTextMessage_BuffersUntilFinalContinuation()
    {
        var reassembler = new WebSocketMessageReassembler();

        var r1 = reassembler.Record(Conn, Frame(WebSocketOpcode.Text, fin: false, "Hello, "));
        Assert.Null(r1);
        Assert.Equal(1, reassembler.PendingCount);

        var r2 = reassembler.Record(Conn, Frame(WebSocketOpcode.Continuation, fin: false, "cruel "));
        Assert.Null(r2);

        var r3 = reassembler.Record(Conn, Frame(WebSocketOpcode.Continuation, fin: true, "world!"));
        Assert.NotNull(r3);
        Assert.Equal("Hello, cruel world!", r3!.Text);
        Assert.Equal(3, r3.FrameCount);
        Assert.True(r3.IsFragmented);
        Assert.Equal(0, reassembler.PendingCount);
    }

    [Fact]
    public void Record_FragmentedBinaryMessage_ConcatenatesPayloadBytesInOrder()
    {
        var reassembler = new WebSocketMessageReassembler();

        reassembler.Record(Conn, BinaryFrame(WebSocketOpcode.Binary, fin: false, [0x01, 0x02]));
        reassembler.Record(Conn, BinaryFrame(WebSocketOpcode.Continuation, fin: false, [0x03, 0x04]));
        var result = reassembler.Record(Conn, BinaryFrame(WebSocketOpcode.Continuation, fin: true, [0x05]));

        Assert.NotNull(result);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 }, result!.Payload);
        Assert.Equal(WebSocketOpcode.Binary, result.Opcode);
        Assert.Equal(3, result.FrameCount);
    }

    [Fact]
    public void Record_ContinuationWithoutStartFrame_IsDroppedSilently()
    {
        var reassembler = new WebSocketMessageReassembler();

        var result = reassembler.Record(Conn, Frame(WebSocketOpcode.Continuation, fin: true, "orphan"));

        Assert.Null(result);
        Assert.Equal(0, reassembler.PendingCount);
    }

    // ── Control frames interleaved with fragmentation (RFC 6455 §5.4) ───────────

    [Fact]
    public void Record_PingInterleavedDuringFragmentedMessage_DoesNotDisturbReassembly()
    {
        var reassembler = new WebSocketMessageReassembler();

        reassembler.Record(Conn, Frame(WebSocketOpcode.Text, fin: false, "part1"));

        var pingResult = reassembler.Record(Conn, ControlFrame(WebSocketOpcode.Ping));
        Assert.NotNull(pingResult);
        Assert.Equal(WebSocketOpcode.Ping, pingResult!.Opcode);
        Assert.False(pingResult.IsFragmented);

        // The in-flight text fragmentation must still be intact after the ping.
        Assert.Equal(1, reassembler.PendingCount);

        var final = reassembler.Record(Conn, Frame(WebSocketOpcode.Continuation, fin: true, "part2"));
        Assert.NotNull(final);
        Assert.Equal("part1part2", final!.Text);
        Assert.Equal(2, final.FrameCount);
    }

    [Fact]
    public void Record_CloseFrame_ReturnsImmediatelyAndIsNeverFragmented()
    {
        var reassembler = new WebSocketMessageReassembler();

        var result = reassembler.Record(Conn, ControlFrame(WebSocketOpcode.Close));

        Assert.NotNull(result);
        Assert.Equal(WebSocketOpcode.Close, result!.Opcode);
        Assert.Equal(1, result.FrameCount);
    }

    // ── Multi-connection isolation ───────────────────────────────────────────────

    [Fact]
    public void Record_TwoConnections_ReassembleIndependently()
    {
        var reassembler = new WebSocketMessageReassembler();
        const string connA = "a";
        const string connB = "b";

        reassembler.Record(connA, Frame(WebSocketOpcode.Text, fin: false, "A1"));
        reassembler.Record(connB, Frame(WebSocketOpcode.Text, fin: false, "B1"));

        Assert.Equal(2, reassembler.PendingCount);

        var resultA = reassembler.Record(connA, Frame(WebSocketOpcode.Continuation, fin: true, "A2"));
        Assert.Equal("A1A2", resultA!.Text);
        Assert.Equal(1, reassembler.PendingCount); // connB still pending

        var resultB = reassembler.Record(connB, Frame(WebSocketOpcode.Continuation, fin: true, "B2"));
        Assert.Equal("B1B2", resultB!.Text);
        Assert.Equal(0, reassembler.PendingCount);
    }

    // ── Clear / Reset ─────────────────────────────────────────────────────────

    [Fact]
    public void Clear_DiscardsInFlightFragmentationForConnection()
    {
        var reassembler = new WebSocketMessageReassembler();
        reassembler.Record(Conn, Frame(WebSocketOpcode.Text, fin: false, "abandoned"));
        Assert.Equal(1, reassembler.PendingCount);

        reassembler.Clear(Conn);

        Assert.Equal(0, reassembler.PendingCount);
        // A subsequent continuation for the cleared connection is dropped, not stitched in.
        var result = reassembler.Record(Conn, Frame(WebSocketOpcode.Continuation, fin: true, "orphaned"));
        Assert.Null(result);
    }

    [Fact]
    public void Reset_ClearsAllConnections()
    {
        var reassembler = new WebSocketMessageReassembler();
        reassembler.Record("a", Frame(WebSocketOpcode.Text, fin: false, "x"));
        reassembler.Record("b", Frame(WebSocketOpcode.Text, fin: false, "y"));
        Assert.Equal(2, reassembler.PendingCount);

        reassembler.Reset();

        Assert.Equal(0, reassembler.PendingCount);
    }
}
