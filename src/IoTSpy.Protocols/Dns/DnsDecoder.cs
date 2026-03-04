using System.Buffers.Binary;
using System.Net;
using System.Text;
using IoTSpy.Core.Interfaces;

namespace IoTSpy.Protocols.Dns;

/// <summary>
/// Decodes raw bytes into DNS messages. Supports standard DNS (RFC 1035) and mDNS (RFC 6762).
/// DNS messages are typically carried in UDP datagrams, so each buffer contains exactly one message.
/// </summary>
public sealed class DnsDecoder : IProtocolDecoder<DnsMessage>
{
    /// <summary>
    /// DNS has a fixed 12-byte header. We sniff by checking that the OPCODE field (bits 11-14
    /// of the flags word) is 0-2 and that the question/answer counts look reasonable.
    /// </summary>
    public bool CanDecode(ReadOnlySpan<byte> header)
    {
        if (header.Length < 12) return false;
        var flags = BinaryPrimitives.ReadUInt16BigEndian(header[2..]);
        var opCode = (flags >> 11) & 0x0F;
        if (opCode > 2) return false;
        var qdCount = BinaryPrimitives.ReadUInt16BigEndian(header[4..]);
        return qdCount is > 0 and <= 256;
    }

    public Task<IReadOnlyList<DnsMessage>> DecodeAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        var messages = new List<DnsMessage>();
        if (TryDecode(data.Span, out var msg))
            messages.Add(msg);
        return Task.FromResult<IReadOnlyList<DnsMessage>>(messages);
    }

    public bool TryDecode(ReadOnlySpan<byte> span, out DnsMessage message, bool isMdns = false)
    {
        message = default!;
        if (span.Length < 12) return false;

        var transactionId = BinaryPrimitives.ReadUInt16BigEndian(span);
        var flags = BinaryPrimitives.ReadUInt16BigEndian(span[2..]);
        var qdCount = BinaryPrimitives.ReadUInt16BigEndian(span[4..]);
        var anCount = BinaryPrimitives.ReadUInt16BigEndian(span[6..]);
        var nsCount = BinaryPrimitives.ReadUInt16BigEndian(span[8..]);
        var arCount = BinaryPrimitives.ReadUInt16BigEndian(span[10..]);

        var isResponse = (flags & 0x8000) != 0;
        var opCode = (byte)((flags >> 11) & 0x0F);
        var authoritative = (flags & 0x0400) != 0;
        var truncated = (flags & 0x0200) != 0;
        var recursionDesired = (flags & 0x0100) != 0;
        var recursionAvailable = (flags & 0x0080) != 0;
        var responseCode = (byte)(flags & 0x000F);

        var pos = 12;

        // Questions
        var questions = new List<DnsQuestion>();
        for (var i = 0; i < qdCount && pos < span.Length; i++)
        {
            var name = ReadDomainName(span, ref pos);
            if (pos + 4 > span.Length) break;
            var qType = (DnsRecordType)BinaryPrimitives.ReadUInt16BigEndian(span[pos..]);
            pos += 2;
            var qClass = BinaryPrimitives.ReadUInt16BigEndian(span[pos..]);
            pos += 2;
            questions.Add(new DnsQuestion(name, qType, qClass));
        }

        // Resource records
        var answers = ReadResourceRecords(span, anCount, ref pos);
        var authority = ReadResourceRecords(span, nsCount, ref pos);
        var additional = ReadResourceRecords(span, arCount, ref pos);

        message = new DnsMessage
        {
            TransactionId = transactionId,
            IsResponse = isResponse,
            OpCode = opCode,
            Authoritative = authoritative,
            Truncated = truncated,
            RecursionDesired = recursionDesired,
            RecursionAvailable = recursionAvailable,
            ResponseCode = responseCode,
            IsMdns = isMdns,
            Questions = questions,
            Answers = answers,
            Authority = authority,
            Additional = additional,
            TotalLength = span.Length,
            RawBytes = span.ToArray()
        };

        return true;
    }

    // ── Resource record parsing ──────────────────────────────────────────────

    private static List<DnsResourceRecord> ReadResourceRecords(
        ReadOnlySpan<byte> span, int count, ref int pos)
    {
        var records = new List<DnsResourceRecord>();
        for (var i = 0; i < count && pos < span.Length; i++)
        {
            var name = ReadDomainName(span, ref pos);
            if (pos + 10 > span.Length) break;

            var rrType = (DnsRecordType)BinaryPrimitives.ReadUInt16BigEndian(span[pos..]);
            pos += 2;
            var rrClass = BinaryPrimitives.ReadUInt16BigEndian(span[pos..]);
            pos += 2;
            var ttl = BinaryPrimitives.ReadUInt32BigEndian(span[pos..]);
            pos += 4;
            var rdLength = BinaryPrimitives.ReadUInt16BigEndian(span[pos..]);
            pos += 2;

            byte[] rData = [];
            if (pos + rdLength <= span.Length)
            {
                rData = span.Slice(pos, rdLength).ToArray();
            }

            var dataString = DecodeRData(rrType, rData, span, pos);
            pos += rdLength;

            records.Add(new DnsResourceRecord
            {
                Name = name,
                Type = rrType,
                Class = rrClass,
                Ttl = ttl,
                RData = rData,
                DataString = dataString
            });
        }
        return records;
    }

    // ── Domain name decoding (with label compression) ────────────────────────

    private static string ReadDomainName(ReadOnlySpan<byte> span, ref int pos)
    {
        var sb = new StringBuilder();
        var jumped = false;
        var savedPos = -1;
        var maxJumps = 64; // safety against malformed pointers

        while (pos < span.Length && maxJumps-- > 0)
        {
            var len = span[pos];

            if (len == 0)
            {
                pos++;
                break;
            }

            // Pointer (2 high bits set)
            if ((len & 0xC0) == 0xC0)
            {
                if (pos + 1 >= span.Length) break;
                if (!jumped) savedPos = pos + 2;
                var offset = ((len & 0x3F) << 8) | span[pos + 1];
                pos = offset;
                jumped = true;
                continue;
            }

            // Label
            pos++;
            if (pos + len > span.Length) break;
            if (sb.Length > 0) sb.Append('.');
            sb.Append(Encoding.ASCII.GetString(span.Slice(pos, len)));
            pos += len;
        }

        if (jumped && savedPos >= 0)
            pos = savedPos;

        return sb.ToString();
    }

    // ── RData interpretation ─────────────────────────────────────────────────

    private static string? DecodeRData(DnsRecordType type, byte[] rData, ReadOnlySpan<byte> fullMessage, int rdataOffset)
    {
        return type switch
        {
            DnsRecordType.A when rData.Length == 4 =>
                new IPAddress(rData).ToString(),

            DnsRecordType.AAAA when rData.Length == 16 =>
                new IPAddress(rData).ToString(),

            DnsRecordType.CNAME or DnsRecordType.NS or DnsRecordType.PTR =>
                DecodeDomainNameFromRData(fullMessage, rdataOffset),

            DnsRecordType.MX when rData.Length >= 4 =>
                $"pref={BinaryPrimitives.ReadUInt16BigEndian(rData)} {DecodeDomainNameFromRData(fullMessage, rdataOffset + 2)}",

            DnsRecordType.TXT => DecodeTxtRecord(rData),

            DnsRecordType.SRV when rData.Length >= 8 =>
                $"priority={BinaryPrimitives.ReadUInt16BigEndian(rData)} " +
                $"weight={BinaryPrimitives.ReadUInt16BigEndian(rData.AsSpan(2))} " +
                $"port={BinaryPrimitives.ReadUInt16BigEndian(rData.AsSpan(4))} " +
                $"{DecodeDomainNameFromRData(fullMessage, rdataOffset + 6)}",

            _ => null
        };
    }

    private static string? DecodeDomainNameFromRData(ReadOnlySpan<byte> fullMessage, int offset)
    {
        if (offset >= fullMessage.Length) return null;
        var pos = offset;
        return ReadDomainName(fullMessage, ref pos);
    }

    private static string DecodeTxtRecord(byte[] rData)
    {
        var sb = new StringBuilder();
        var pos = 0;
        while (pos < rData.Length)
        {
            var len = rData[pos++];
            if (pos + len > rData.Length) break;
            if (sb.Length > 0) sb.Append("; ");
            sb.Append(Encoding.UTF8.GetString(rData, pos, len));
            pos += len;
        }
        return sb.ToString();
    }
}
