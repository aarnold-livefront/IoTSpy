using IoTSpy.Protocols.Modbus;
using Xunit;

namespace IoTSpy.Protocols.Tests;

public class ModbusDecoderTests
{
    private readonly ModbusDecoder _decoder = new();

    // ── CanDecode ────────────────────────────────────────────────────────────

    [Fact]
    public void CanDecode_TooShort_ReturnsFalse()
    {
        Assert.False(_decoder.CanDecode([0x00, 0x01, 0x00, 0x00, 0x00]));
    }

    [Fact]
    public void CanDecode_ValidModbusTcpHeader_ReturnsTrue()
    {
        // Transaction=0x0001, Protocol=0x0000, Length=6, UnitId=1
        Assert.True(_decoder.CanDecode([0x00, 0x01, 0x00, 0x00, 0x00, 0x06, 0x01]));
    }

    [Fact]
    public void CanDecode_NonModbusProtocolId_ReturnsFalse()
    {
        // Protocol ID must be 0x0000 for Modbus TCP
        Assert.False(_decoder.CanDecode([0x00, 0x01, 0x00, 0x01, 0x00, 0x06, 0x01]));
    }

    [Fact]
    public void CanDecode_InvalidLength_ReturnsFalse()
    {
        // Length=0 is invalid
        Assert.False(_decoder.CanDecode([0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x01]));
    }

    // ── DecodeAsync: Read Holding Registers request ──────────────────────────

    [Fact]
    public async Task DecodeAsync_ReadHoldingRegistersRequest_Decodes()
    {
        // Transaction=1, Protocol=0, Length=6, UnitId=1, FC=3, StartAddr=0, Qty=10
        byte[] data =
        [
            0x00, 0x01, // Transaction ID
            0x00, 0x00, // Protocol ID
            0x00, 0x06, // Length
            0x01,       // Unit ID
            0x03,       // Function code: Read Holding Registers
            0x00, 0x00, // Start address: 0
            0x00, 0x0A  // Quantity: 10
        ];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(messages);
        var msg = messages[0];
        Assert.Equal(1, msg.TransactionId);
        Assert.Equal(0, msg.ProtocolId);
        Assert.Equal(1, msg.UnitId);
        Assert.Equal(0x03, msg.FunctionCode);
        Assert.Equal("Read Holding Registers", msg.FunctionName);
        Assert.False(msg.IsException);
        Assert.Equal((ushort)0, msg.StartAddress);
        Assert.Equal((ushort)10, msg.Quantity);
    }

    // ── DecodeAsync: Read Holding Registers response ─────────────────────────

    [Fact]
    public async Task DecodeAsync_ReadHoldingRegistersResponse_DecodesRegisters()
    {
        // Transaction=1, Protocol=0, Length=7, UnitId=1, FC=3, ByteCount=4, Regs=[100, 200]
        byte[] data =
        [
            0x00, 0x01,
            0x00, 0x00,
            0x00, 0x07, // Length = 1 (unit) + 1 (fc) + 1 (byte count) + 4 (registers)
            0x01,       // Unit ID
            0x03,       // FC: Read Holding Registers
            0x04,       // Byte count: 4
            0x00, 0x64, // Register 0: 100
            0x00, 0xC8  // Register 1: 200
        ];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(messages);
        var msg = messages[0];
        Assert.Equal(0x03, msg.FunctionCode);
        Assert.Equal((byte)4, msg.ByteCount);
        Assert.NotNull(msg.RegisterValues);
        Assert.Equal(2, msg.RegisterValues!.Length);
        Assert.Equal((ushort)100, msg.RegisterValues[0]);
        Assert.Equal((ushort)200, msg.RegisterValues[1]);
    }

    // ── DecodeAsync: Write Single Register ────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_WriteSingleRegister_DecodesAddressAndValue()
    {
        // FC=6, Address=1, Value=0x0003
        byte[] data =
        [
            0x00, 0x01,
            0x00, 0x00,
            0x00, 0x06,
            0x01,       // Unit ID
            0x06,       // FC: Write Single Register
            0x00, 0x01, // Address: 1
            0x00, 0x03  // Value: 3
        ];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(messages);
        var msg = messages[0];
        Assert.Equal(0x06, msg.FunctionCode);
        Assert.Equal("Write Single Register", msg.FunctionName);
        Assert.Equal((ushort)1, msg.StartAddress);
        Assert.NotNull(msg.RegisterValues);
        Assert.Equal((ushort)3, msg.RegisterValues![0]);
    }

    // ── DecodeAsync: Exception response ──────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_ExceptionResponse_DecodesExceptionCode()
    {
        // FC=0x83 (exception for FC 3), exception code=2 (Illegal Data Address)
        byte[] data =
        [
            0x00, 0x01,
            0x00, 0x00,
            0x00, 0x03,
            0x01,       // Unit ID
            0x83,       // Exception for FC 3
            0x02        // Exception code: Illegal Data Address
        ];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(messages);
        var msg = messages[0];
        Assert.True(msg.IsException);
        Assert.Equal(0x03, msg.FunctionCode); // base function code
        Assert.Equal((byte)0x02, msg.ExceptionCode);
        Assert.Equal("Illegal Data Address", msg.ExceptionName);
    }

    // ── DecodeAsync: Read Coils request ──────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_ReadCoilsRequest_DecodesAddressAndQuantity()
    {
        // FC=1, StartAddr=0, Qty=10
        byte[] data =
        [
            0x00, 0x01,
            0x00, 0x00,
            0x00, 0x06,
            0x01,       // Unit ID
            0x01,       // FC: Read Coils
            0x00, 0x00, // Start address: 0
            0x00, 0x0A  // Quantity: 10
        ];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(messages);
        var msg = messages[0];
        Assert.Equal("Read Coils", msg.FunctionName);
        Assert.Equal((ushort)0, msg.StartAddress);
        Assert.Equal((ushort)10, msg.Quantity);
    }

    // ── DecodeAsync: Multiple frames ─────────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_MultipleFrames_DecodesAll()
    {
        byte[] data =
        [
            // Frame 1: Read Holding Registers request
            0x00, 0x01, 0x00, 0x00, 0x00, 0x06, 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A,
            // Frame 2: Write Single Register
            0x00, 0x02, 0x00, 0x00, 0x00, 0x06, 0x01, 0x06, 0x00, 0x01, 0x00, 0x03
        ];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Equal(2, messages.Count);
        Assert.Equal(1, messages[0].TransactionId);
        Assert.Equal(2, messages[1].TransactionId);
    }

    // ── DecodeAsync: Edge cases ──────────────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_TruncatedFrame_ReturnsEmpty()
    {
        // Length says 6 but only 3 bytes follow
        byte[] data = [0x00, 0x01, 0x00, 0x00, 0x00, 0x06, 0x01, 0x03, 0x00];

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Empty(messages);
    }

    [Fact]
    public async Task DecodeAsync_EmptyBuffer_ReturnsEmpty()
    {
        var messages = await _decoder.DecodeAsync(Array.Empty<byte>(), TestContext.Current.CancellationToken);

        Assert.Empty(messages);
    }

    // ── Model properties ────────────────────────────────────────────────────

    [Fact]
    public void ToString_FormatsCorrectly()
    {
        var msg = new ModbusMessage
        {
            UnitId = 1,
            FunctionCode = 0x03,
            FunctionName = "Read Holding Registers",
            StartAddress = 0,
            Quantity = 10
        };

        var str = msg.ToString();
        Assert.Contains("Read Holding Registers", str);
        Assert.Contains("unit=1", str);
    }
}
