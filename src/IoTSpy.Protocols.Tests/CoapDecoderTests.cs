using IoTSpy.Protocols.Coap;
using Xunit;

namespace IoTSpy.Protocols.Tests;

public class CoapDecoderTests
{
    private readonly CoapDecoder _decoder = new();

    // ── CanDecode ────────────────────────────────────────────────────────────

    [Fact]
    public void CanDecode_TooShort_ReturnsFalse()
    {
        Assert.False(_decoder.CanDecode([0x40, 0x01, 0x00]));
    }

    [Fact]
    public void CanDecode_ValidCoapHeader_ReturnsTrue()
    {
        // Ver=1, Type=CON(0), TKL=0, Code=GET(0.01), MessageId=0x0001
        Assert.True(_decoder.CanDecode([0x40, 0x01, 0x00, 0x01]));
    }

    [Fact]
    public void CanDecode_InvalidVersion_ReturnsFalse()
    {
        // Ver=0 is invalid (must be 1)
        Assert.False(_decoder.CanDecode([0x00, 0x01, 0x00, 0x01]));
    }

    [Fact]
    public void CanDecode_InvalidTokenLength_ReturnsFalse()
    {
        // TKL=9 is invalid (max is 8)
        Assert.False(_decoder.CanDecode([0x49, 0x01, 0x00, 0x01]));
    }

    // ── DecodeAsync: GET request ─────────────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_GetRequest_DecodesCorrectly()
    {
        // Ver=1, Type=CON(0), TKL=0, Code=0.01(GET), MID=0x0001
        byte[] data = [0x40, 0x01, 0x00, 0x01];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(messages);
        var msg = messages[0];
        Assert.Equal(1, msg.Version);
        Assert.Equal(CoapMessageType.Confirmable, msg.Type);
        Assert.Equal(0, msg.TokenLength);
        Assert.True(msg.IsRequest);
        Assert.Equal("GET", msg.CodeName);
        Assert.Equal(1, msg.MessageId);
    }

    // ── DecodeAsync: with token ──────────────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_WithToken_DecodesToken()
    {
        // Ver=1, Type=CON, TKL=4, Code=GET, MID=0x0001, Token=0xAABBCCDD
        byte[] data = [0x44, 0x01, 0x00, 0x01, 0xAA, 0xBB, 0xCC, 0xDD];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(messages);
        var msg = messages[0];
        Assert.Equal(4, msg.TokenLength);
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD }, msg.Token);
    }

    // ── DecodeAsync: with options ────────────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_WithUriPathOption_DecodesPath()
    {
        // GET with Uri-Path option = "test"
        // Uri-Path is option 11. Delta=11, Length=4
        byte[] data =
        [
            0x40, 0x01, 0x00, 0x01,     // Header: GET, MID=1
            0xB4,                         // Option: delta=11 (Uri-Path), length=4
            (byte)'t', (byte)'e', (byte)'s', (byte)'t' // "test"
        ];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(messages);
        Assert.Equal("test", messages[0].UriPath);
    }

    [Fact]
    public async Task DecodeAsync_WithMultipleUriPathSegments_JoinsPath()
    {
        // GET /sensor/temp
        // First option: delta=11 (Uri-Path), length=6, value="sensor"
        // Second option: delta=0 (same option number 11), length=4, value="temp"
        byte[] data =
        [
            0x40, 0x01, 0x00, 0x01,
            0xB6,                                                       // delta=11, length=6
            (byte)'s', (byte)'e', (byte)'n', (byte)'s', (byte)'o', (byte)'r',
            0x04,                                                       // delta=0, length=4
            (byte)'t', (byte)'e', (byte)'m', (byte)'p'
        ];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(messages);
        Assert.Equal("sensor/temp", messages[0].UriPath);
    }

    // ── DecodeAsync: with payload ────────────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_WithPayload_DecodesPayload()
    {
        // GET with payload marker (0xFF) + payload
        byte[] data =
        [
            0x40, 0x01, 0x00, 0x01,
            0xFF,                                    // Payload marker
            (byte)'{', (byte)'"', (byte)'t', (byte)'"', (byte)':',
            (byte)'1', (byte)'}'
        ];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(messages);
        Assert.Equal("{\"t\":1}", messages[0].PayloadString);
    }

    // ── DecodeAsync: response ────────────────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_Response_DecodesStatusCode()
    {
        // ACK 2.05 Content
        byte[] data = [0x60, 0x45, 0x00, 0x01]; // Type=ACK(2), Code=2.05

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(messages);
        var msg = messages[0];
        Assert.True(msg.IsResponse);
        Assert.Equal(CoapMessageType.Acknowledgement, msg.Type);
        Assert.Equal("Content", msg.CodeName);
        Assert.Equal("2.05", msg.CodeString);
    }

    // ── DecodeAsync: edge cases ──────────────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_EmptyBuffer_ReturnsEmpty()
    {
        var messages = await _decoder.DecodeAsync(Array.Empty<byte>(), TestContext.Current.CancellationToken);

        Assert.Empty(messages);
    }

    [Fact]
    public async Task DecodeAsync_TruncatedToken_ReturnsEmpty()
    {
        // TKL=4 but only 2 token bytes
        byte[] data = [0x44, 0x01, 0x00, 0x01, 0xAA, 0xBB];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Empty(messages);
    }
}
