using Xunit;
using IoTSpy.Protocols.Dns;

namespace IoTSpy.Protocols.Tests;

public class DnsDecoderTests
{
    private readonly DnsDecoder _decoder = new();

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal DNS query packet for a given domain name and record type.
    /// </summary>
    private static byte[] BuildDnsQuery(string name, ushort type = 0x0001 /* A */, ushort id = 0xABCD)
    {
        var parts = name.Split('.');
        var nameBytes = new List<byte>();
        foreach (var part in parts)
        {
            nameBytes.Add((byte)part.Length);
            nameBytes.AddRange(System.Text.Encoding.ASCII.GetBytes(part));
        }
        nameBytes.Add(0x00); // root label

        var payload = new List<byte>
        {
            (byte)(id >> 8), (byte)(id & 0xFF), // Transaction ID
            0x01, 0x00,                          // Flags: query, RD=1
            0x00, 0x01,                          // QDCOUNT=1
            0x00, 0x00,                          // ANCOUNT=0
            0x00, 0x00,                          // NSCOUNT=0
            0x00, 0x00,                          // ARCOUNT=0
        };
        payload.AddRange(nameBytes);
        payload.Add((byte)(type >> 8)); payload.Add((byte)(type & 0xFF)); // QTYPE
        payload.Add(0x00); payload.Add(0x01);                             // QCLASS=IN

        return [.. payload];
    }

    // ── CanDecode ────────────────────────────────────────────────────────────

    [Fact]
    public void CanDecode_TooShort_ReturnsFalse()
    {
        Assert.False(_decoder.CanDecode([0x00, 0x01, 0x00, 0x00]));
    }

    [Fact]
    public void CanDecode_ValidDnsQuery_ReturnsTrue()
    {
        var data = BuildDnsQuery("example.com");
        Assert.True(_decoder.CanDecode(data.AsSpan(0, 12)));
    }

    [Fact]
    public void CanDecode_InvalidOpcode_ReturnsFalse()
    {
        // opCode 3 (reserved) in bits 11-14 of flags word
        // flags = 0x1800 => opCode = (0x1800 >> 11) & 0x0F = 3
        byte[] header = [0xAB, 0xCD, 0x18, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
        Assert.False(_decoder.CanDecode(header));
    }

    [Fact]
    public void CanDecode_ZeroQuestionCount_ReturnsFalse()
    {
        // QDCOUNT=0 → CanDecode should return false (requires > 0)
        byte[] header = [0xAB, 0xCD, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
        Assert.False(_decoder.CanDecode(header));
    }

    // ── TryDecode: basic query ───────────────────────────────────────────────

    [Fact]
    public void TryDecode_SimpleDnsQuery_ParsesQuestion()
    {
        var data = BuildDnsQuery("example.com", id: 0x1234);

        var result = _decoder.TryDecode(data, out var msg);

        Assert.True(result);
        Assert.Equal(0x1234, msg.TransactionId);
        Assert.False(msg.IsResponse);
        Assert.True(msg.RecursionDesired);
        Assert.Single(msg.Questions);
        Assert.Equal("example.com", msg.Questions[0].Name);
        Assert.Equal(DnsRecordType.A, msg.Questions[0].Type);
        Assert.Empty(msg.Answers);
    }

    [Fact]
    public void TryDecode_AaaaQuery_ParsesCorrectType()
    {
        var data = BuildDnsQuery("ipv6.example.com", type: 0x001C /* AAAA */);

        var result = _decoder.TryDecode(data, out var msg);

        Assert.True(result);
        Assert.Single(msg.Questions);
        Assert.Equal(DnsRecordType.AAAA, msg.Questions[0].Type);
        Assert.Equal("ipv6.example.com", msg.Questions[0].Name);
    }

    // ── TryDecode: response with A record ────────────────────────────────────

    [Fact]
    public void TryDecode_DnsResponse_WithARecord_DecodesIpAddress()
    {
        // Build a DNS response for "test.local" → 192.168.1.1
        var nameBytes = new byte[] { 4, (byte)'t', (byte)'e', (byte)'s', (byte)'t', 5, (byte)'l', (byte)'o', (byte)'c', (byte)'a', (byte)'l', 0 };
        // Use pointer compression for answer: 0xC0 0x0C points back to offset 12 (the question name)
        var data = new List<byte>
        {
            0x00, 0x01,  // ID
            0x81, 0x80,  // Flags: QR=1 (response), AA=0, RD=1, RA=1
            0x00, 0x01,  // QDCOUNT=1
            0x00, 0x01,  // ANCOUNT=1
            0x00, 0x00,  // NSCOUNT=0
            0x00, 0x00   // ARCOUNT=0
        };
        data.AddRange(nameBytes); // question name at offset 12
        data.AddRange([0x00, 0x01, 0x00, 0x01]); // QTYPE=A, QCLASS=IN
        // Answer RR: pointer to name, TYPE=A, CLASS=IN, TTL=300, RDLENGTH=4, RDATA=192.168.1.1
        data.AddRange([0xC0, 0x0C]); // name pointer to offset 12
        data.AddRange([0x00, 0x01]); // TYPE=A
        data.AddRange([0x00, 0x01]); // CLASS=IN
        data.AddRange([0x00, 0x00, 0x01, 0x2C]); // TTL=300
        data.AddRange([0x00, 0x04]); // RDLENGTH=4
        data.AddRange([192, 168, 1, 1]); // RDATA=192.168.1.1

        var result = _decoder.TryDecode([.. data], out var msg);

        Assert.True(result);
        Assert.True(msg.IsResponse);
        Assert.Single(msg.Answers);
        Assert.Equal(DnsRecordType.A, msg.Answers[0].Type);
        Assert.Equal("192.168.1.1", msg.Answers[0].DataString);
        Assert.Equal(300u, msg.Answers[0].Ttl);
    }

    // ── TryDecode: mDNS flag ─────────────────────────────────────────────────

    [Fact]
    public void TryDecode_WithMdnsFlag_SetsMdnsProperty()
    {
        var data = BuildDnsQuery("device.local");

        var result = _decoder.TryDecode(data, out var msg, isMdns: true);

        Assert.True(result);
        Assert.True(msg.IsMdns);
    }

    // ── TryDecode: edge cases ────────────────────────────────────────────────

    [Fact]
    public void TryDecode_TooShort_ReturnsFalse()
    {
        Assert.False(_decoder.TryDecode([0x00, 0x01], out _));
    }

    [Fact]
    public void TryDecode_MultiLabelDomain_ParsesCorrectly()
    {
        var data = BuildDnsQuery("sub.example.co.uk");

        var result = _decoder.TryDecode(data, out var msg);

        Assert.True(result);
        Assert.Equal("sub.example.co.uk", msg.Questions[0].Name);
    }

    // ── EDNS0 OPT record (RFC 6891) ──────────────────────────────────────────

    private static byte[] BuildDnsQueryWithEdns(string name, ushort udpPayloadSize = 4096, bool doBit = false, ushort id = 0x1234)
    {
        var parts = name.Split('.');
        var nameBytes = new List<byte>();
        foreach (var part in parts)
        {
            nameBytes.Add((byte)part.Length);
            nameBytes.AddRange(System.Text.Encoding.ASCII.GetBytes(part));
        }
        nameBytes.Add(0x00);

        var payload = new List<byte>
        {
            (byte)(id >> 8), (byte)(id & 0xFF),  // Transaction ID
            0x01, 0x00,                            // Flags: query, RD=1
            0x00, 0x01,                            // QDCOUNT=1
            0x00, 0x00,                            // ANCOUNT=0
            0x00, 0x00,                            // NSCOUNT=0
            0x00, 0x01                             // ARCOUNT=1 (OPT record)
        };

        payload.AddRange(nameBytes);
        payload.AddRange([(byte)0x00, 0x01]); // Type A
        payload.AddRange([(byte)0x00, 0x01]); // Class IN

        // OPT pseudo-RR
        payload.Add(0x00); // Name = root
        payload.AddRange([(byte)0x00, 0x29]); // Type = OPT (41)
        payload.Add((byte)(udpPayloadSize >> 8));
        payload.Add((byte)(udpPayloadSize & 0xFF)); // Class = UDP payload size
        // TTL: [extRcode=0][version=0][flags] where flags bit 15 = DO
        var flags = (ushort)(doBit ? 0x8000 : 0x0000);
        payload.AddRange([(byte)0x00, (byte)0x00, (byte)(flags >> 8), (byte)(flags & 0xFF)]);
        payload.AddRange([(byte)0x00, 0x00]); // RDLENGTH = 0

        return payload.ToArray();
    }

    [Fact]
    public void TryDecode_WithEdnsOptRecord_PopulatesEdnsRecord()
    {
        var data = BuildDnsQueryWithEdns("example.com", udpPayloadSize: 4096);

        var result = _decoder.TryDecode(data, out var msg);

        Assert.True(result);
        Assert.NotNull(msg.EdnsRecord);
        Assert.Equal(4096, msg.EdnsRecord.UdpPayloadSize);
        Assert.Equal(0, msg.EdnsRecord.Version);
        Assert.Equal(0, msg.EdnsRecord.ExtendedRcode);
        Assert.False(msg.EdnsRecord.DoBit);
    }

    [Fact]
    public void TryDecode_WithEdnsAndDoBit_DoBitIsTrue()
    {
        var data = BuildDnsQueryWithEdns("example.com", doBit: true);

        _decoder.TryDecode(data, out var msg);

        Assert.NotNull(msg.EdnsRecord);
        Assert.True(msg.EdnsRecord.DoBit);
    }

    [Fact]
    public void TryDecode_WithoutEdns_EdnsRecordIsNull()
    {
        var data = BuildDnsQuery("example.com");

        _decoder.TryDecode(data, out var msg);

        Assert.Null(msg.EdnsRecord);
    }

    // ── DNSSEC: AD bit + RRSIG detection ─────────────────────────────────────

    /// <summary>
    /// Builds a DNS response with a single resource record of the given type/rdata,
    /// optionally with the AD (Authenticated Data) flag set.
    /// </summary>
    private static byte[] BuildDnsResponseWithRecord(string name, ushort rrType, byte[] rdata, bool adBit = false, ushort id = 0x0001)
    {
        var parts = name.Split('.');
        var nameBytes = new List<byte>();
        foreach (var part in parts)
        {
            nameBytes.Add((byte)part.Length);
            nameBytes.AddRange(System.Text.Encoding.ASCII.GetBytes(part));
        }
        nameBytes.Add(0x00);

        // Flags: QR=1, RD=1, RA=1, AD=<adBit>
        var flags = (ushort)(0x8000 | 0x0100 | 0x0080 | (adBit ? 0x0020 : 0x0000));

        var payload = new List<byte>
        {
            (byte)(id >> 8), (byte)(id & 0xFF),
            (byte)(flags >> 8), (byte)(flags & 0xFF),
            0x00, 0x01, // QDCOUNT=1
            0x00, 0x01, // ANCOUNT=1
            0x00, 0x00, // NSCOUNT=0
            0x00, 0x00  // ARCOUNT=0
        };
        payload.AddRange(nameBytes);
        payload.AddRange([0x00, 0x01, 0x00, 0x01]); // QTYPE=A, QCLASS=IN

        // Answer RR: pointer to question name, given type/class IN/TTL/rdata
        payload.AddRange([0xC0, 0x0C]);
        payload.Add((byte)(rrType >> 8)); payload.Add((byte)(rrType & 0xFF));
        payload.AddRange([0x00, 0x01]); // CLASS=IN
        payload.AddRange([0x00, 0x00, 0x01, 0x2C]); // TTL=300
        payload.Add((byte)(rdata.Length >> 8)); payload.Add((byte)(rdata.Length & 0xFF));
        payload.AddRange(rdata);

        return [.. payload];
    }

    [Fact]
    public void TryDecode_AdBitSet_ExposesAdBitSetTrue()
    {
        var data = BuildDnsResponseWithRecord("example.com", 0x0001 /* A */, [192, 168, 1, 1], adBit: true);

        var result = _decoder.TryDecode(data, out var msg);

        Assert.True(result);
        Assert.True(msg.AdBitSet);
    }

    [Fact]
    public void TryDecode_AdBitNotSet_ExposesAdBitSetFalse()
    {
        var data = BuildDnsResponseWithRecord("example.com", 0x0001 /* A */, [192, 168, 1, 1], adBit: false);

        var result = _decoder.TryDecode(data, out var msg);

        Assert.True(result);
        Assert.False(msg.AdBitSet);
    }

    [Fact]
    public void TryDecode_WithRrsigRecord_HasDnssecRecordsIsTrue()
    {
        // Minimal (nonsense but well-formed) RRSIG RDATA — content isn't parsed, only type matters.
        byte[] rrsigRdata = [0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x01, 0x2C, 0x00, 0x00, 0x00, 0x00];
        var data = BuildDnsResponseWithRecord("example.com", 46 /* RRSIG */, rrsigRdata);

        var result = _decoder.TryDecode(data, out var msg);

        Assert.True(result);
        Assert.Single(msg.Answers);
        Assert.Equal(DnsRecordType.RRSIG, msg.Answers[0].Type);
        Assert.True(msg.HasDnssecRecords);
    }

    [Fact]
    public void TryDecode_WithoutRrsigRecord_HasDnssecRecordsIsFalse()
    {
        var data = BuildDnsResponseWithRecord("example.com", 0x0001 /* A */, [192, 168, 1, 1]);

        var result = _decoder.TryDecode(data, out var msg);

        Assert.True(result);
        Assert.False(msg.HasDnssecRecords);
    }

    // ── DecodeAsync wraps TryDecode ──────────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_ValidQuery_ReturnsOneMessage()
    {
        var data = BuildDnsQuery("test.com");

        var messages = await _decoder.DecodeAsync(data, TestContext.Current.CancellationToken);

        Assert.Single(messages);
        Assert.Equal("test.com", messages[0].Questions[0].Name);
    }

    [Fact]
    public async Task DecodeAsync_InvalidData_ReturnsEmpty()
    {
        // Only 5 bytes — too short to be a DNS message
        var messages = await _decoder.DecodeAsync(new byte[] { 0x00, 0x01, 0x00, 0x00, 0x00 }, TestContext.Current.CancellationToken);

        Assert.Empty(messages);
    }
}
