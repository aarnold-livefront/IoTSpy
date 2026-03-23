using IoTSpy.Core.Enums;

namespace IoTSpy.Core.Models;

public class WebSocketFrame
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CaptureId { get; set; }
    public bool Fin { get; set; }
    public WebSocketOpcode Opcode { get; set; }
    public bool Masked { get; set; }
    public long PayloadLength { get; set; }
    public string? PayloadText { get; set; }
    public byte[]? PayloadBinary { get; set; }
    public bool IsFromClient { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public int SequenceNumber { get; set; }
}
