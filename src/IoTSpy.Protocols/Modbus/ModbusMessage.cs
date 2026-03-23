namespace IoTSpy.Protocols.Modbus;

/// <summary>
/// Represents a decoded Modbus TCP frame.
/// </summary>
public sealed class ModbusMessage
{
    // MBAP header
    public ushort TransactionId { get; init; }
    public ushort ProtocolId { get; init; }
    public byte UnitId { get; init; }

    // PDU
    public byte FunctionCode { get; init; }
    public string FunctionName { get; init; } = string.Empty;
    public bool IsException { get; init; }
    public byte? ExceptionCode { get; init; }
    public string? ExceptionName { get; init; }

    // Function-specific decoded fields
    public ushort? StartAddress { get; init; }
    public ushort? Quantity { get; init; }
    public byte? ByteCount { get; init; }
    public ushort[]? RegisterValues { get; init; }
    public byte[]? CoilValues { get; init; }

    public int TotalLength { get; init; }
    public byte[]? RawBytes { get; init; }

    public bool IsRequest => !IsException && StartAddress.HasValue && Quantity.HasValue && RegisterValues == null && CoilValues == null;
    public bool IsResponse => !IsException && (RegisterValues != null || CoilValues != null || ByteCount.HasValue);

    public override string ToString()
    {
        var type = IsException ? "Exception" : IsRequest ? "Request" : "Response";
        return $"Modbus {type} unit={UnitId} func={FunctionName}({FunctionCode:X2})" +
               (StartAddress.HasValue ? $" addr={StartAddress}" : "") +
               (Quantity.HasValue ? $" qty={Quantity}" : "");
    }
}
