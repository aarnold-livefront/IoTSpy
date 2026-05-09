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

    // ── Block-wise transfer (RFC 7959) ───────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_WithBlock2Option_DecodesBlockOption()
    {
        // GET with Block2 option (option 23): num=1, M=false, SZX=6 (1024 B)
        // Delta=23 (>=13 so extended): nibble=0xD, extended=10 (23-13), len=1, value=0x16
        // value byte: num=1 (bits[7:4]=0001), M=0 (bit3), SZX=6 (bits[2:0])
        // raw = (1 << 4) | 0 | 6 = 0x16
        byte[] data =
        [
            0x40, 0x01, 0x00, 0x01,   // CON GET MID=1
            0xD1,                      // delta-ext nibble (0xD=13), len=1
            0x0A,                      // extended delta = 10  → option 10+13=23 (Block2)
            0x16                       // num=1, M=false, SZX=6
        ];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(messages);
        var block = messages[0].Block2;
        Assert.NotNull(block);
        Assert.Equal(1u, block.Num);
        Assert.False(block.More);
        Assert.Equal(6, block.Szx);
        Assert.Equal(1024, block.BlockSize);
    }

    [Fact]
    public async Task DecodeAsync_WithBlock2MoreBit_MoreIsTrue()
    {
        // Block2 num=0, M=true, SZX=6 → raw = (0<<4) | (1<<3) | 6 = 0x0E
        byte[] data =
        [
            0x40, 0x01, 0x00, 0x01,
            0xD1, 0x0A, 0x0E
        ];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(messages);
        var block = messages[0].Block2;
        Assert.NotNull(block);
        Assert.Equal(0u, block.Num);
        Assert.True(block.More);
    }

    // ── Observe option (RFC 7641) ────────────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_WithObserveRegister_ObserveValueIsZero()
    {
        // Observe option = 6, delta=6, len=1, value=0x00 (register)
        byte[] data =
        [
            0x40, 0x01, 0x00, 0x01,   // GET CON
            0x61, 0x00                 // delta=6 (Observe), len=1, value=0
        ];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(messages);
        Assert.Equal(0u, messages[0].ObserveValue);
    }

    [Fact]
    public async Task DecodeAsync_WithObserveSequenceNumber_DecodesValue()
    {
        // Observe notification with sequence 42 (0x2A)
        byte[] data =
        [
            0x60, 0x45, 0x00, 0x02,   // ACK 2.05 Content
            0x61, 0x2A                 // delta=6 (Observe), len=1, value=42
        ];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(messages);
        Assert.Equal(42u, messages[0].ObserveValue);
    }

    [Fact]
    public async Task DecodeAsync_WithoutObserveOption_ObserveValueIsNull()
    {
        byte[] data = [0x40, 0x01, 0x00, 0x01];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(messages);
        Assert.Null(messages[0].ObserveValue);
    }

    // ── Well-known/core (resource discovery) ─────────────────────────────────

    [Fact]
    public async Task DecodeAsync_WellKnownCoreRequest_IsWellKnownCoreTrue()
    {
        // GET /.well-known/core
        // Uri-Path option 11: ".well-known" (delta=11, len=11) then "core" (delta=0, len=4)
        byte[] wellKnown = System.Text.Encoding.ASCII.GetBytes(".well-known");
        byte[] core = System.Text.Encoding.ASCII.GetBytes("core");

        var data = new List<byte> { 0x40, 0x01, 0x00, 0x01 };
        // First segment: delta=11 (0xB), length=11 (0xB) → 0xBB
        data.Add(0xBB);
        data.AddRange(wellKnown);
        // Second segment: delta=0, length=4 → 0x04
        data.Add(0x04);
        data.AddRange(core);

        var messages = await _decoder.DecodeAsync(data.ToArray(), TestContext.Current.CancellationToken);

        Assert.Single(messages);
        Assert.True(messages[0].IsWellKnownCore);
        Assert.Equal(".well-known/core", messages[0].UriPath);
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
