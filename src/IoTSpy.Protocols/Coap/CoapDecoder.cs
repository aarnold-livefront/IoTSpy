using System.Buffers.Binary;
using IoTSpy.Core.Interfaces;

namespace IoTSpy.Protocols.Coap;

/// <summary>
/// Decodes raw bytes into CoAP messages per RFC 7252.
/// CoAP uses a compact binary format over UDP (typically port 5683).
/// </summary>
public sealed class CoapDecoder : IProtocolDecoder<CoapMessage>
{
    /// <summary>
    /// Sniffs for CoAP: version must be 1 (bits 6-7 of first byte), token length 0-8.
    /// </summary>
    public bool CanDecode(ReadOnlySpan<byte> header)
    {
        if (header.Length < 4) return false;
        var version = (header[0] >> 6) & 0x03;
        if (version != 1) return false;
        var tokenLength = header[0] & 0x0F;
        return tokenLength <= 8;
    }

    public Task<IReadOnlyList<CoapMessage>> DecodeAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        var messages = new List<CoapMessage>();

        if (data.Length >= 4 && TryDecode(data.Span, out var msg))
            messages.Add(msg);

        return Task.FromResult<IReadOnlyList<CoapMessage>>(messages);
    }

    private static bool TryDecode(ReadOnlySpan<byte> span, out CoapMessage message)
    {
        message = default!;

        if (span.Length < 4) return false;

        var byte0 = span[0];
        var version = (byte)((byte0 >> 6) & 0x03);
        if (version != 1) return false;

        var type = (CoapMessageType)((byte0 >> 4) & 0x03);
        var tokenLength = (byte)(byte0 & 0x0F);
        if (tokenLength > 8) return false;

        var code = span[1];
        var messageId = BinaryPrimitives.ReadUInt16BigEndian(span[2..]);

        var pos = 4;

        // Token
        if (pos + tokenLength > span.Length) return false;
        var token = span.Slice(pos, tokenLength).ToArray();
        pos += tokenLength;

        // Options
        var options = new List<CoapOption>();
        uint currentOptionNumber = 0;

        while (pos < span.Length && span[pos] != 0xFF)
        {
            if (!TryReadOption(span, ref pos, ref currentOptionNumber, out var option))
                break;
            options.Add(option);
        }

        // Payload
        byte[]? payload = null;
        if (pos < span.Length && span[pos] == 0xFF)
        {
            pos++; // skip payload marker
            if (pos < span.Length)
                payload = span[pos..].ToArray();
            pos = span.Length;
        }

        message = new CoapMessage
        {
            Version = version,
            Type = type,
            TokenLength = tokenLength,
            Code = code,
            MessageId = messageId,
            Token = token,
            Options = options,
            Payload = payload,
            TotalLength = span.Length,
            RawBytes = span.ToArray()
        };

        return true;
    }

    private static bool TryReadOption(ReadOnlySpan<byte> span, ref int pos, ref uint currentOptionNumber, out CoapOption option)
    {
        option = default!;

        if (pos >= span.Length) return false;

        var byte0 = span[pos++];
        var delta = (uint)((byte0 >> 4) & 0x0F);
        var length = (uint)(byte0 & 0x0F);

        // Extended delta
        if (delta == 13)
        {
            if (pos >= span.Length) return false;
            delta = (uint)(span[pos++] + 13);
        }
        else if (delta == 14)
        {
            if (pos + 1 >= span.Length) return false;
            delta = (uint)(BinaryPrimitives.ReadUInt16BigEndian(span[pos..]) + 269);
            pos += 2;
        }
        else if (delta == 15) return false; // reserved

        // Extended length
        if (length == 13)
        {
            if (pos >= span.Length) return false;
            length = (uint)(span[pos++] + 13);
        }
        else if (length == 14)
        {
            if (pos + 1 >= span.Length) return false;
            length = (uint)(BinaryPrimitives.ReadUInt16BigEndian(span[pos..]) + 269);
            pos += 2;
        }
        else if (length == 15) return false; // reserved

        currentOptionNumber += delta;

        if (pos + length > span.Length) return false;
        var value = span.Slice(pos, (int)length).ToArray();
        pos += (int)length;

        option = new CoapOption
        {
            Number = (CoapOptionNumber)currentOptionNumber,
            Value = value
        };

        return true;
    }
}
