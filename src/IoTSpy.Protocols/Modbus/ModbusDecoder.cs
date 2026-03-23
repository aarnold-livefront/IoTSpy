using System.Buffers.Binary;
using IoTSpy.Core.Interfaces;

namespace IoTSpy.Protocols.Modbus;

/// <summary>
/// Decodes Modbus TCP/IP frames per the Modbus Application Protocol Specification.
/// Modbus TCP wraps Modbus PDUs in a 7-byte MBAP header (transaction ID, protocol ID, length, unit ID).
/// </summary>
public sealed class ModbusDecoder : IProtocolDecoder<ModbusMessage>
{
    /// <summary>
    /// Sniffs for Modbus TCP MBAP header: bytes 2-3 must be 0x0000 (protocol identifier for Modbus).
    /// Length field (bytes 4-5) must be reasonable (1-253).
    /// </summary>
    public bool CanDecode(ReadOnlySpan<byte> header)
    {
        if (header.Length < 7) return false;
        // Protocol identifier must be 0x0000 for Modbus TCP
        var protocolId = BinaryPrimitives.ReadUInt16BigEndian(header[2..]);
        if (protocolId != 0) return false;
        // Length field: number of following bytes including unit ID
        var length = BinaryPrimitives.ReadUInt16BigEndian(header[4..]);
        return length is >= 1 and <= 253;
    }

    public Task<IReadOnlyList<ModbusMessage>> DecodeAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        var messages = new List<ModbusMessage>();
        var span = data.Span;
        var offset = 0;

        while (offset < span.Length && !ct.IsCancellationRequested)
        {
            if (!TryDecodeFrame(span[offset..], out var msg, out var consumed))
                break;

            messages.Add(msg);
            offset += consumed;
        }

        return Task.FromResult<IReadOnlyList<ModbusMessage>>(messages);
    }

    private static bool TryDecodeFrame(ReadOnlySpan<byte> span, out ModbusMessage message, out int consumed)
    {
        message = default!;
        consumed = 0;

        if (span.Length < 8) return false; // 7 MBAP + at least 1 function code

        // MBAP header
        var transactionId = BinaryPrimitives.ReadUInt16BigEndian(span);
        var protocolId = BinaryPrimitives.ReadUInt16BigEndian(span[2..]);
        if (protocolId != 0) return false;

        var length = BinaryPrimitives.ReadUInt16BigEndian(span[4..]);
        if (length < 1 || length > 253) return false;

        var totalLength = 6 + length; // MBAP header (6 bytes without unit ID) + length (includes unit ID)
        if (span.Length < totalLength) return false;

        var unitId = span[6];
        var functionCode = span[7];
        var isException = (functionCode & 0x80) != 0;
        var baseFunctionCode = isException ? (byte)(functionCode & 0x7F) : functionCode;

        byte? exceptionCode = null;
        if (isException && length >= 3)
            exceptionCode = span[8];

        // Extract PDU data (after unit ID + function code)
        var pduDataLength = length - 2; // subtract unit ID and function code
        byte[]? pduData = null;
        if (pduDataLength > 0 && span.Length >= 8 + pduDataLength)
            pduData = span.Slice(8, pduDataLength).ToArray();

        // Decode function-specific details
        var details = DecodeFunctionDetails(baseFunctionCode, isException, pduData);

        message = new ModbusMessage
        {
            TransactionId = transactionId,
            ProtocolId = protocolId,
            UnitId = unitId,
            FunctionCode = baseFunctionCode,
            FunctionName = GetFunctionName(baseFunctionCode),
            IsException = isException,
            ExceptionCode = exceptionCode,
            ExceptionName = exceptionCode.HasValue ? GetExceptionName(exceptionCode.Value) : null,
            StartAddress = details.StartAddress,
            Quantity = details.Quantity,
            ByteCount = details.ByteCount,
            RegisterValues = details.RegisterValues,
            CoilValues = details.CoilValues,
            TotalLength = totalLength,
            RawBytes = span[..totalLength].ToArray()
        };

        consumed = totalLength;
        return true;
    }

    private static FunctionDetails DecodeFunctionDetails(byte functionCode, bool isException, byte[]? data)
    {
        var details = new FunctionDetails();
        if (data == null || data.Length == 0 || isException) return details;

        switch (functionCode)
        {
            // Read Coils / Discrete Inputs (request: startAddr + quantity)
            case 0x01 or 0x02 when data.Length >= 4:
                details.StartAddress = BinaryPrimitives.ReadUInt16BigEndian(data);
                details.Quantity = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(2));
                break;

            // Read Coils / Discrete Inputs (response: byte count + coil status)
            case 0x01 or 0x02 when data.Length >= 1 && data[0] < data.Length:
                details.ByteCount = data[0];
                details.CoilValues = data.AsSpan(1, data[0]).ToArray();
                break;

            // Read Holding/Input Registers (request: startAddr + quantity)
            case 0x03 or 0x04 when data.Length >= 4 && data.Length == 4:
                details.StartAddress = BinaryPrimitives.ReadUInt16BigEndian(data);
                details.Quantity = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(2));
                break;

            // Read Holding/Input Registers (response: byte count + register values)
            case 0x03 or 0x04 when data.Length >= 1 && data[0] > 0:
                details.ByteCount = data[0];
                var regCount = data[0] / 2;
                var regs = new ushort[regCount];
                for (var i = 0; i < regCount && 1 + i * 2 + 1 < data.Length; i++)
                    regs[i] = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(1 + i * 2));
                details.RegisterValues = regs;
                break;

            // Write Single Coil (address + value)
            case 0x05 when data.Length >= 4:
                details.StartAddress = BinaryPrimitives.ReadUInt16BigEndian(data);
                details.Quantity = 1;
                break;

            // Write Single Register (address + value)
            case 0x06 when data.Length >= 4:
                details.StartAddress = BinaryPrimitives.ReadUInt16BigEndian(data);
                details.RegisterValues = [BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(2))];
                break;

            // Write Multiple Coils/Registers (address + quantity + byte count + values)
            case 0x0F or 0x10 when data.Length >= 5:
                details.StartAddress = BinaryPrimitives.ReadUInt16BigEndian(data);
                details.Quantity = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(2));
                details.ByteCount = data[4];
                break;
        }

        return details;
    }

    private static string GetFunctionName(byte code) => code switch
    {
        0x01 => "Read Coils",
        0x02 => "Read Discrete Inputs",
        0x03 => "Read Holding Registers",
        0x04 => "Read Input Registers",
        0x05 => "Write Single Coil",
        0x06 => "Write Single Register",
        0x07 => "Read Exception Status",
        0x08 => "Diagnostics",
        0x0B => "Get Comm Event Counter",
        0x0C => "Get Comm Event Log",
        0x0F => "Write Multiple Coils",
        0x10 => "Write Multiple Registers",
        0x11 => "Report Server ID",
        0x14 => "Read File Record",
        0x15 => "Write File Record",
        0x16 => "Mask Write Register",
        0x17 => "Read/Write Multiple Registers",
        0x18 => "Read FIFO Queue",
        0x2B => "Encapsulated Interface Transport",
        _ => $"Function 0x{code:X2}"
    };

    private static string GetExceptionName(byte code) => code switch
    {
        0x01 => "Illegal Function",
        0x02 => "Illegal Data Address",
        0x03 => "Illegal Data Value",
        0x04 => "Server Device Failure",
        0x05 => "Acknowledge",
        0x06 => "Server Device Busy",
        0x08 => "Memory Parity Error",
        0x0A => "Gateway Path Unavailable",
        0x0B => "Gateway Target Device Failed to Respond",
        _ => $"Exception 0x{code:X2}"
    };

    private struct FunctionDetails
    {
        public ushort? StartAddress;
        public ushort? Quantity;
        public byte? ByteCount;
        public ushort[]? RegisterValues;
        public byte[]? CoilValues;
    }
}
