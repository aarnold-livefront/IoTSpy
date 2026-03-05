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

    // ── DecodeAsync wraps TryDecode ──────────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_ValidQuery_ReturnsOneMessage()
    {
        var data = BuildDnsQuery("test.com");

        var messages = await _decoder.DecodeAsync(data);

        Assert.Single(messages);
        Assert.Equal("test.com", messages[0].Questions[0].Name);
    }

    [Fact]
    public async Task DecodeAsync_InvalidData_ReturnsEmpty()
    {
        // Only 5 bytes — too short to be a DNS message
        var messages = await _decoder.DecodeAsync(new byte[] { 0x00, 0x01, 0x00, 0x00, 0x00 });

        Assert.Empty(messages);
    }
}
