using IoTSpy.Core.Enums;
using IoTSpy.Protocols.WebSocket;
using Xunit;

namespace IoTSpy.Protocols.Tests;

public class WebSocketDecoderTests
{
    private readonly WebSocketDecoder _decoder = new();

    // ── CanDecode ────────────────────────────────────────────────────────────

    [Fact]
    public void CanDecode_TooShort_ReturnsFalse()
    {
        Assert.False(_decoder.CanDecode([0x81]));
    }

    [Fact]
    public void CanDecode_TextFrame_ReturnsTrue()
    {
        // FIN=1, opcode=1 (text), mask=0, length=5
        Assert.True(_decoder.CanDecode([0x81, 0x05]));
    }

    [Fact]
    public void CanDecode_BinaryFrame_ReturnsTrue()
    {
        Assert.True(_decoder.CanDecode([0x82, 0x03]));
    }

    [Fact]
    public void CanDecode_PingFrame_ReturnsTrue()
    {
        Assert.True(_decoder.CanDecode([0x89, 0x00]));
    }

    [Fact]
    public void CanDecode_InvalidOpcode_ReturnsFalse()
    {
        // opcode 0x05 is reserved
        Assert.False(_decoder.CanDecode([0x85, 0x00]));
    }

    // ── DecodeAsync: Text frame ──────────────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_UnmaskedTextFrame_DecodesPayload()
    {
        // FIN=1, opcode=1 (text), no mask, length=5, payload="Hello"
        byte[] data = [0x81, 0x05, 0x48, 0x65, 0x6C, 0x6C, 0x6F];

        var frames = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(frames);
        var frame = frames[0];
        Assert.True(frame.Fin);
        Assert.Equal(WebSocketOpcode.Text, frame.Opcode);
        Assert.False(frame.Masked);
        Assert.Equal(5, frame.PayloadLength);
        Assert.Equal("Hello", frame.PayloadText);
        Assert.Equal(7, frame.TotalLength);
    }

    [Fact]
    public async Task DecodeAsync_MaskedTextFrame_UnmasksPayload()
    {
        // FIN=1, opcode=1 (text), masked, length=5
        // Mask key: 0x37 0xFA 0x21 0x3D
        // Masked payload of "Hello": H^37=7F, e^FA=9F, l^21=4D, l^3D=51, o^37=58
        byte[] data = [0x81, 0x85, 0x37, 0xFA, 0x21, 0x3D, 0x7F, 0x9F, 0x4D, 0x51, 0x58];

        var frames = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(frames);
        var frame = frames[0];
        Assert.True(frame.Masked);
        Assert.Equal("Hello", frame.PayloadText);
    }

    // ── DecodeAsync: Binary frame ────────────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_BinaryFrame_DecodesPayload()
    {
        // FIN=1, opcode=2 (binary), no mask, length=3
        byte[] data = [0x82, 0x03, 0x01, 0x02, 0x03];

        var frames = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(frames);
        var frame = frames[0];
        Assert.Equal(WebSocketOpcode.Binary, frame.Opcode);
        Assert.Equal(3, frame.PayloadLength);
        Assert.Null(frame.PayloadText);
    }

    // ── DecodeAsync: Close frame ─────────────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_CloseFrame_DecodesCloseCode()
    {
        // FIN=1, opcode=8 (close), no mask, length=2, close code=1000
        byte[] data = [0x88, 0x02, 0x03, 0xE8];

        var frames = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(frames);
        var frame = frames[0];
        Assert.Equal(WebSocketOpcode.Close, frame.Opcode);
        Assert.Equal((ushort)1000, frame.CloseCode);
    }

    // ── DecodeAsync: Ping/Pong ───────────────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_PingFrame_ReturnsCorrectOpcode()
    {
        byte[] data = [0x89, 0x00];

        var frames = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(frames);
        Assert.Equal(WebSocketOpcode.Ping, frames[0].Opcode);
        Assert.True(frames[0].IsControl);
    }

    [Fact]
    public async Task DecodeAsync_PongFrame_ReturnsCorrectOpcode()
    {
        byte[] data = [0x8A, 0x00];

        var frames = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(frames);
        Assert.Equal(WebSocketOpcode.Pong, frames[0].Opcode);
    }

    // ── DecodeAsync: Extended payload length ──────────────────────────────────

    [Fact]
    public async Task DecodeAsync_ExtendedLength16Bit_DecodesCorrectly()
    {
        // FIN=1, opcode=1, no mask, length=126, actual length=256 (0x0100)
        var payload = new byte[256];
        for (var i = 0; i < 256; i++) payload[i] = (byte)'A';

        var data = new byte[4 + 256];
        data[0] = 0x81;
        data[1] = 126;
        data[2] = 0x01; // 256 big-endian
        data[3] = 0x00;
        Array.Copy(payload, 0, data, 4, 256);

        var frames = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(frames);
        Assert.Equal(256, frames[0].PayloadLength);
    }

    // ── DecodeAsync: Multiple frames ─────────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_MultipleFrames_DecodesAll()
    {
        // Ping + Pong
        byte[] data = [0x89, 0x00, 0x8A, 0x00];

        var frames = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Equal(2, frames.Count);
        Assert.Equal(WebSocketOpcode.Ping, frames[0].Opcode);
        Assert.Equal(WebSocketOpcode.Pong, frames[1].Opcode);
    }

    // ── DecodeAsync: Truncated ───────────────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_TruncatedFrame_ReturnsEmpty()
    {
        // Says length=10 but only has 2 bytes of payload
        byte[] data = [0x81, 0x0A, 0x48, 0x65];

        var frames = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Empty(frames);
    }

    [Fact]
    public async Task DecodeAsync_EmptyBuffer_ReturnsEmpty()
    {
        var frames = await _decoder.DecodeAsync(Array.Empty<byte>(), TestContext.Current.CancellationToken);

        Assert.Empty(frames);
    }

    // ── Model properties ────────────────────────────────────────────────────

    [Fact]
    public void IsControl_ControlFrames_ReturnsTrue()
    {
        var close = new WebSocketDecodedFrame { Opcode = WebSocketOpcode.Close };
        var ping = new WebSocketDecodedFrame { Opcode = WebSocketOpcode.Ping };
        var pong = new WebSocketDecodedFrame { Opcode = WebSocketOpcode.Pong };

        Assert.True(close.IsControl);
        Assert.True(ping.IsControl);
        Assert.True(pong.IsControl);
    }

    [Fact]
    public void IsData_DataFrames_ReturnsTrue()
    {
        var text = new WebSocketDecodedFrame { Opcode = WebSocketOpcode.Text };
        var binary = new WebSocketDecodedFrame { Opcode = WebSocketOpcode.Binary };
        var cont = new WebSocketDecodedFrame { Opcode = WebSocketOpcode.Continuation };

        Assert.True(text.IsData);
        Assert.True(binary.IsData);
        Assert.True(cont.IsData);
    }
}
