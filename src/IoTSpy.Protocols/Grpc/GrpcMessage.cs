namespace IoTSpy.Protocols.Grpc;

/// <summary>
/// Represents a decoded gRPC or gRPC-Web Length-Prefixed Message frame.
/// </summary>
public sealed class GrpcMessage
{
    public bool IsCompressed { get; init; }
    public int MessageLength { get; init; }
    public byte[] Payload { get; init; } = [];
    public int TotalLength { get; init; }
    public IReadOnlyList<ProtobufField> Fields { get; init; } = [];
    public byte[]? RawBytes { get; init; }

    /// <summary>Flag byte value from the LPM header.</summary>
    public GrpcFrameType FrameType { get; init; } = GrpcFrameType.Data;

    /// <summary>True when the framing came from a gRPC-Web trailer frame (flag 0x80).</summary>
    public bool IsTrailerFrame => FrameType == GrpcFrameType.Trailer;

    public override string ToString() =>
        $"gRPC msg len={MessageLength} compressed={IsCompressed} fields={Fields.Count} frame={FrameType}";
}

/// <summary>
/// A single protobuf field decoded without schema, optionally enriched with a field name.
/// </summary>
public sealed class ProtobufField
{
    public int FieldNumber { get; init; }
    public ProtobufWireType WireType { get; init; }
    public string Value { get; init; } = string.Empty;
    public byte[]? RawBytes { get; init; }

    /// <summary>
    /// Human-readable field name resolved from an uploaded .proto schema, or null if unknown.
    /// </summary>
    public string? FieldName { get; init; }

    public override string ToString() =>
        FieldName is not null
            ? $"field {FieldNumber} ({WireType}) [{FieldName}]: {Value}"
            : $"field {FieldNumber} ({WireType}): {Value}";
}

public enum ProtobufWireType
{
    Varint = 0,
    Fixed64 = 1,
    LengthDelimited = 2,
    StartGroup = 3,  // deprecated
    EndGroup = 4,    // deprecated
    Fixed32 = 5
}

public enum GrpcFrameType
{
    /// <summary>Standard data frame (flag byte 0x00).</summary>
    Data = 0,
    /// <summary>gRPC-Web trailer frame (flag byte 0x80).</summary>
    Trailer = 0x80
}
