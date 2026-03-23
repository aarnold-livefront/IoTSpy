namespace IoTSpy.Protocols.Grpc;

/// <summary>
/// Represents a decoded gRPC Length-Prefixed Message.
/// </summary>
public sealed class GrpcMessage
{
    public bool IsCompressed { get; init; }
    public int MessageLength { get; init; }
    public byte[] Payload { get; init; } = [];
    public int TotalLength { get; init; }
    public IReadOnlyList<ProtobufField> Fields { get; init; } = [];
    public byte[]? RawBytes { get; init; }

    public override string ToString() =>
        $"gRPC msg len={MessageLength} compressed={IsCompressed} fields={Fields.Count}";
}

/// <summary>
/// A single protobuf field decoded without schema.
/// </summary>
public sealed class ProtobufField
{
    public int FieldNumber { get; init; }
    public ProtobufWireType WireType { get; init; }
    public string Value { get; init; } = string.Empty;
    public byte[]? RawBytes { get; init; }

    public override string ToString() => $"field {FieldNumber} ({WireType}): {Value}";
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
