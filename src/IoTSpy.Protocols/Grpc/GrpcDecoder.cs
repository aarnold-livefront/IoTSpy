using System.Buffers.Binary;
using System.Text;
using IoTSpy.Core.Interfaces;

namespace IoTSpy.Protocols.Grpc;

/// <summary>
/// Decodes gRPC and gRPC-Web messages from HTTP/2 request/response bodies.
/// gRPC uses Length-Prefixed Message framing: 1-byte flag + 4-byte big-endian length.
/// gRPC-Web reuses the same framing; flag 0x80 signals a trailer frame.
/// </summary>
public sealed class GrpcDecoder : IProtocolDecoder<GrpcMessage>
{
    /// <summary>
    /// Sniffs for gRPC / gRPC-Web Length-Prefixed Message framing.
    /// Flag byte 0x00 = uncompressed data, 0x01 = compressed data, 0x80 = gRPC-Web trailer.
    /// </summary>
    public bool CanDecode(ReadOnlySpan<byte> header)
    {
        if (header.Length < 5) return false;
        var flag = header[0];
        if (flag != 0x00 && flag != 0x01 && flag != 0x80) return false;
        var messageLength = BinaryPrimitives.ReadUInt32BigEndian(header[1..]);
        return messageLength <= 16 * 1024 * 1024;
    }

    public Task<IReadOnlyList<GrpcMessage>> DecodeAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken ct = default)
        => DecodeAsync(data, fieldMap: null, ct);

    /// <summary>
    /// Decodes messages and applies field names from <paramref name="fieldMap"/> when provided.
    /// The field map is keyed by field number (from a parsed .proto schema).
    /// </summary>
    public Task<IReadOnlyList<GrpcMessage>> DecodeAsync(
        ReadOnlyMemory<byte> data,
        IReadOnlyDictionary<int, string>? fieldMap,
        CancellationToken ct = default)
    {
        var messages = new List<GrpcMessage>();
        var span = data.Span;
        var offset = 0;

        while (offset < span.Length - 4 && !ct.IsCancellationRequested)
        {
            if (!TryDecodeMessage(span[offset..], fieldMap, out var msg, out var consumed))
                break;

            messages.Add(msg);
            offset += consumed;
        }

        return Task.FromResult<IReadOnlyList<GrpcMessage>>(messages);
    }

    private static bool TryDecodeMessage(
        ReadOnlySpan<byte> span,
        IReadOnlyDictionary<int, string>? fieldMap,
        out GrpcMessage message,
        out int consumed)
    {
        message = default!;
        consumed = 0;

        if (span.Length < 5) return false;

        var flag = span[0];
        var frameType = flag == 0x80 ? GrpcFrameType.Trailer : GrpcFrameType.Data;
        var compressed = flag == 0x01;
        var messageLength = (int)BinaryPrimitives.ReadUInt32BigEndian(span[1..]);

        if (messageLength < 0 || messageLength > 16 * 1024 * 1024) return false;

        var totalLength = 5 + messageLength;
        if (span.Length < totalLength) return false;

        var payload = span[5..totalLength].ToArray();

        var fields = new List<ProtobufField>();
        if (!compressed && frameType == GrpcFrameType.Data)
            TryParseProtobufFields(payload, fields, fieldMap);

        message = new GrpcMessage
        {
            IsCompressed = compressed,
            MessageLength = messageLength,
            Payload = payload,
            TotalLength = totalLength,
            Fields = fields,
            RawBytes = span[..totalLength].ToArray(),
            FrameType = frameType
        };

        consumed = totalLength;
        return true;
    }

    /// <summary>
    /// Best-effort protobuf field extraction. Parses tag-value pairs without a .proto schema.
    /// </summary>
    private static void TryParseProtobufFields(
        byte[] data,
        List<ProtobufField> fields,
        IReadOnlyDictionary<int, string>? fieldMap)
    {
        var pos = 0;
        var maxFields = 50;

        while (pos < data.Length && fields.Count < maxFields)
        {
            if (!TryDecodeVarint(data, ref pos, out var tag))
                break;

            var fieldNumber = (int)(tag >> 3);
            var wireType = (ProtobufWireType)(tag & 0x07);

            if (fieldNumber <= 0 || fieldNumber > 536870911) break;

            string? resolvedName = null;
            fieldMap?.TryGetValue(fieldNumber, out resolvedName);

            switch (wireType)
            {
                case ProtobufWireType.Varint:
                    if (!TryDecodeVarint(data, ref pos, out var varintValue)) return;
                    fields.Add(new ProtobufField
                    {
                        FieldNumber = fieldNumber,
                        WireType = wireType,
                        Value = varintValue.ToString(),
                        FieldName = resolvedName
                    });
                    break;

                case ProtobufWireType.Fixed64:
                    if (pos + 8 > data.Length) return;
                    var f64 = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(pos));
                    pos += 8;
                    fields.Add(new ProtobufField
                    {
                        FieldNumber = fieldNumber,
                        WireType = wireType,
                        Value = f64.ToString(),
                        FieldName = resolvedName
                    });
                    break;

                case ProtobufWireType.LengthDelimited:
                    if (!TryDecodeVarint(data, ref pos, out var len)) return;
                    if (len < 0 || pos + len > data.Length) return;
                    var bytes = data.AsSpan(pos, (int)len).ToArray();
                    pos += (int)len;
                    string? strVal = null;
                    try
                    {
                        var s = Encoding.UTF8.GetString(bytes);
                        if (s.All(c => !char.IsControl(c) || c is '\n' or '\r' or '\t'))
                            strVal = s;
                    }
                    catch { /* not valid UTF-8 */ }

                    fields.Add(new ProtobufField
                    {
                        FieldNumber = fieldNumber,
                        WireType = wireType,
                        Value = strVal ?? $"[{bytes.Length} bytes]",
                        RawBytes = bytes,
                        FieldName = resolvedName
                    });
                    break;

                case ProtobufWireType.Fixed32:
                    if (pos + 4 > data.Length) return;
                    var f32 = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pos));
                    pos += 4;
                    fields.Add(new ProtobufField
                    {
                        FieldNumber = fieldNumber,
                        WireType = wireType,
                        Value = f32.ToString(),
                        FieldName = resolvedName
                    });
                    break;

                default:
                    return;
            }
        }
    }

    private static bool TryDecodeVarint(byte[] data, ref int pos, out long value)
    {
        value = 0;
        var shift = 0;
        while (pos < data.Length)
        {
            var b = data[pos++];
            value |= (long)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return true;
            shift += 7;
            if (shift > 63) return false;
        }
        return false;
    }
}
