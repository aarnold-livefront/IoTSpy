using IoTSpy.Protocols.Grpc;
using Xunit;

namespace IoTSpy.Protocols.Tests;

public class GrpcDecoderTests
{
    private readonly GrpcDecoder _decoder = new();

    // ── CanDecode ────────────────────────────────────────────────────────────

    [Fact]
    public void CanDecode_TooShort_ReturnsFalse()
    {
        Assert.False(_decoder.CanDecode([0x00, 0x00, 0x00]));
    }

    [Fact]
    public void CanDecode_ValidUncompressedHeader_ReturnsTrue()
    {
        // compressed=0, length=10
        Assert.True(_decoder.CanDecode([0x00, 0x00, 0x00, 0x00, 0x0A]));
    }

    [Fact]
    public void CanDecode_ValidCompressedHeader_ReturnsTrue()
    {
        // compressed=1, length=10
        Assert.True(_decoder.CanDecode([0x01, 0x00, 0x00, 0x00, 0x0A]));
    }

    [Fact]
    public void CanDecode_InvalidCompressedFlag_ReturnsFalse()
    {
        // compressed=2 is invalid
        Assert.False(_decoder.CanDecode([0x02, 0x00, 0x00, 0x00, 0x0A]));
    }

    [Fact]
    public void CanDecode_TooLargeLength_ReturnsFalse()
    {
        // length > 16MB
        Assert.False(_decoder.CanDecode([0x00, 0x01, 0x00, 0x00, 0x01]));
    }

    // ── DecodeAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_SimpleMessage_DecodesCorrectly()
    {
        // compressed=0, length=5, payload=5 bytes
        byte[] data = [0x00, 0x00, 0x00, 0x00, 0x05, 0x01, 0x02, 0x03, 0x04, 0x05];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(messages);
        var msg = messages[0];
        Assert.False(msg.IsCompressed);
        Assert.Equal(5, msg.MessageLength);
        Assert.Equal(10, msg.TotalLength);
        Assert.Equal(5, msg.Payload.Length);
    }

    [Fact]
    public async Task DecodeAsync_CompressedMessage_SetsFlag()
    {
        byte[] data = [0x01, 0x00, 0x00, 0x00, 0x03, 0xAA, 0xBB, 0xCC];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(messages);
        Assert.True(messages[0].IsCompressed);
        Assert.Empty(messages[0].Fields); // compressed data can't be parsed as protobuf
    }

    [Fact]
    public async Task DecodeAsync_ProtobufVarint_ExtractsField()
    {
        // Protobuf: field 1, varint, value 150
        // Tag: 0x08 (field=1, wire=0), Value: 0x96 0x01 (150)
        byte[] protobufData = [0x08, 0x96, 0x01];
        byte[] data = [0x00, 0x00, 0x00, 0x00, 0x03, .. protobufData];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(messages);
        var msg = messages[0];
        Assert.NotEmpty(msg.Fields);
        Assert.Equal(1, msg.Fields[0].FieldNumber);
        Assert.Equal(ProtobufWireType.Varint, msg.Fields[0].WireType);
        Assert.Equal("150", msg.Fields[0].Value);
    }

    [Fact]
    public async Task DecodeAsync_ProtobufString_ExtractsField()
    {
        // Protobuf: field 2, length-delimited, value "test"
        // Tag: 0x12 (field=2, wire=2), Length: 0x04, Data: "test"
        byte[] protobufData = [0x12, 0x04, (byte)'t', (byte)'e', (byte)'s', (byte)'t'];
        byte[] data = [0x00, 0x00, 0x00, 0x00, 0x06, .. protobufData];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(messages);
        var field = messages[0].Fields.First(f => f.FieldNumber == 2);
        Assert.Equal(ProtobufWireType.LengthDelimited, field.WireType);
        Assert.Equal("test", field.Value);
    }

    [Fact]
    public async Task DecodeAsync_MultipleMessages_DecodesAll()
    {
        byte[] data =
        [
            0x00, 0x00, 0x00, 0x00, 0x02, 0xAA, 0xBB,
            0x00, 0x00, 0x00, 0x00, 0x03, 0xCC, 0xDD, 0xEE
        ];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Equal(2, messages.Count);
        Assert.Equal(2, messages[0].MessageLength);
        Assert.Equal(3, messages[1].MessageLength);
    }

    [Fact]
    public async Task DecodeAsync_TruncatedMessage_ReturnsEmpty()
    {
        // Says length=10 but only 2 bytes of payload
        byte[] data = [0x00, 0x00, 0x00, 0x00, 0x0A, 0x01, 0x02];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Empty(messages);
    }

    [Fact]
    public async Task DecodeAsync_EmptyPayload_DecodesZeroLength()
    {
        byte[] data = [0x00, 0x00, 0x00, 0x00, 0x00];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(messages);
        Assert.Equal(0, messages[0].MessageLength);
        Assert.Empty(messages[0].Payload);
    }

    // ── gRPC-Web trailer frames ──────────────────────────────────────────────

    [Fact]
    public void CanDecode_GrpcWebTrailerFlag_ReturnsTrue()
    {
        // flag=0x80 (gRPC-Web trailer), length=20
        Assert.True(_decoder.CanDecode([0x80, 0x00, 0x00, 0x00, 0x14]));
    }

    [Fact]
    public async Task DecodeAsync_GrpcWebTrailerFrame_SetsFrameType()
    {
        // trailer frame: flag=0x80, length=4, payload = "test"
        byte[] data = [0x80, 0x00, 0x00, 0x00, 0x04, (byte)'t', (byte)'e', (byte)'s', (byte)'t'];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(messages);
        Assert.Equal(GrpcFrameType.Trailer, messages[0].FrameType);
        Assert.True(messages[0].IsTrailerFrame);
        Assert.Empty(messages[0].Fields); // trailers don't parse as protobuf
    }

    [Fact]
    public async Task DecodeAsync_DataFrame_HasDataFrameType()
    {
        byte[] data = [0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(messages);
        Assert.Equal(GrpcFrameType.Data, messages[0].FrameType);
        Assert.False(messages[0].IsTrailerFrame);
    }

    [Fact]
    public async Task DecodeAsync_MixedDataAndTrailer_DecodesAll()
    {
        // data frame (flag=0x00, len=2) followed by trailer (flag=0x80, len=3)
        byte[] data =
        [
            0x00, 0x00, 0x00, 0x00, 0x02, 0xAA, 0xBB,
            0x80, 0x00, 0x00, 0x00, 0x03, (byte)'a', (byte)'b', (byte)'c'
        ];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Equal(2, messages.Count);
        Assert.Equal(GrpcFrameType.Data, messages[0].FrameType);
        Assert.Equal(GrpcFrameType.Trailer, messages[1].FrameType);
    }

    // ── Proto schema field name resolution ───────────────────────────────────

    [Fact]
    public async Task DecodeAsync_WithFieldMap_ResolvesFieldNames()
    {
        // Protobuf: field 1 varint=42, field 2 string="hello"
        byte[] protobuf =
        [
            0x08, 0x2A,                                            // field 1 = 42
            0x12, 0x05, (byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o' // field 2 = "hello"
        ];
        byte[] data = [0x00, 0x00, 0x00, 0x00, (byte)protobuf.Length, .. protobuf];

        var fieldMap = new Dictionary<int, string> { [1] = "device_id", [2] = "name" };
        var messages = await _decoder.DecodeAsync(data, fieldMap, TestContext.Current.CancellationToken);

        Assert.Single(messages);
        var f1 = messages[0].Fields.First(f => f.FieldNumber == 1);
        var f2 = messages[0].Fields.First(f => f.FieldNumber == 2);
        Assert.Equal("device_id", f1.FieldName);
        Assert.Equal("name", f2.FieldName);
    }

    [Fact]
    public async Task DecodeAsync_WithoutFieldMap_FieldNameIsNull()
    {
        byte[] protobuf = [0x08, 0x2A]; // field 1 = 42
        byte[] data = [0x00, 0x00, 0x00, 0x00, 0x02, .. protobuf];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(messages);
        Assert.Null(messages[0].Fields[0].FieldName);
    }
}
