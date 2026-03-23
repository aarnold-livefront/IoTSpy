using System.Buffers.Binary;
using System.Text;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;

namespace IoTSpy.Protocols.WebSocket;

/// <summary>
/// Decodes raw bytes into WebSocket frames per RFC 6455.
/// </summary>
public sealed class WebSocketDecoder : IProtocolDecoder<WebSocketDecodedFrame>
{
    public bool CanDecode(ReadOnlySpan<byte> header)
    {
        if (header.Length < 2) return false;
        var opcode = (byte)(header[0] & 0x0F);
        // Valid opcodes: 0x0-0x2 (data), 0x8-0xA (control)
        return opcode <= 0x02 || (opcode >= 0x08 && opcode <= 0x0A);
    }

    public Task<IReadOnlyList<WebSocketDecodedFrame>> DecodeAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        var frames = new List<WebSocketDecodedFrame>();
        var span = data.Span;
        var offset = 0;

        while (offset < span.Length && !ct.IsCancellationRequested)
        {
            if (!TryDecodeFrame(span[offset..], out var frame, out var consumed))
                break;

            frames.Add(frame);
            offset += consumed;
        }

        return Task.FromResult<IReadOnlyList<WebSocketDecodedFrame>>(frames);
    }

    private static bool TryDecodeFrame(ReadOnlySpan<byte> span, out WebSocketDecodedFrame frame, out int consumed)
    {
        frame = default!;
        consumed = 0;

        if (span.Length < 2) return false;

        var byte0 = span[0];
        var byte1 = span[1];

        var fin = (byte0 & 0x80) != 0;
        var rsv1 = (byte0 & 0x40) != 0;
        var rsv2 = (byte0 & 0x20) != 0;
        var rsv3 = (byte0 & 0x10) != 0;
        var opcode = (WebSocketOpcode)(byte0 & 0x0F);
        var masked = (byte1 & 0x80) != 0;
        long payloadLength = byte1 & 0x7F;

        var headerSize = 2;

        if (payloadLength == 126)
        {
            if (span.Length < 4) return false;
            payloadLength = BinaryPrimitives.ReadUInt16BigEndian(span[2..]);
            headerSize = 4;
        }
        else if (payloadLength == 127)
        {
            if (span.Length < 10) return false;
            payloadLength = (long)BinaryPrimitives.ReadUInt64BigEndian(span[2..]);
            headerSize = 10;
        }

        if (masked) headerSize += 4;

        var totalLength = headerSize + payloadLength;
        if (span.Length < totalLength) return false;

        byte[]? maskKey = null;
        if (masked)
        {
            maskKey = span[(headerSize - 4)..headerSize].ToArray();
        }

        var payloadBytes = span[headerSize..(int)totalLength].ToArray();

        // Unmask if needed
        if (masked && maskKey != null)
        {
            for (var i = 0; i < payloadBytes.Length; i++)
                payloadBytes[i] ^= maskKey[i % 4];
        }

        string? payloadText = null;
        if (opcode is WebSocketOpcode.Text or WebSocketOpcode.Close)
        {
            try { payloadText = Encoding.UTF8.GetString(payloadBytes); }
            catch { /* binary data in text frame */ }
        }

        ushort? closeCode = null;
        string? closeReason = null;
        if (opcode == WebSocketOpcode.Close && payloadBytes.Length >= 2)
        {
            closeCode = BinaryPrimitives.ReadUInt16BigEndian(payloadBytes);
            if (payloadBytes.Length > 2)
                closeReason = Encoding.UTF8.GetString(payloadBytes, 2, payloadBytes.Length - 2);
        }

        frame = new WebSocketDecodedFrame
        {
            Fin = fin,
            Rsv1 = rsv1,
            Rsv2 = rsv2,
            Rsv3 = rsv3,
            Opcode = opcode,
            Masked = masked,
            PayloadLength = payloadLength,
            PayloadBytes = payloadBytes,
            PayloadText = payloadText,
            CloseCode = closeCode,
            CloseReason = closeReason,
            TotalLength = (int)totalLength,
            RawBytes = span[..(int)totalLength].ToArray()
        };

        consumed = (int)totalLength;
        return true;
    }
}
