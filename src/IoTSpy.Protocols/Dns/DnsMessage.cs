namespace IoTSpy.Protocols.Dns;

/// <summary>
/// Represents a decoded DNS message (query or response).
/// Works for both standard DNS (port 53) and mDNS (224.0.0.251:5353).
/// </summary>
public sealed class DnsMessage
{
    public ushort TransactionId { get; init; }
    public bool IsResponse { get; init; }
    public byte OpCode { get; init; }
    public bool Authoritative { get; init; }
    public bool Truncated { get; init; }
    public bool RecursionDesired { get; init; }
    public bool RecursionAvailable { get; init; }
    public byte ResponseCode { get; init; }

    public bool IsMdns { get; init; }

    public IReadOnlyList<DnsQuestion> Questions { get; init; } = [];
    public IReadOnlyList<DnsResourceRecord> Answers { get; init; } = [];
    public IReadOnlyList<DnsResourceRecord> Authority { get; init; } = [];
    public IReadOnlyList<DnsResourceRecord> Additional { get; init; } = [];

    /// <summary>
    /// Parsed EDNS0 OPT pseudo-RR (RFC 6891). Null when no OPT record present.
    /// The OPT record appears in the Additional section with type=41.
    /// </summary>
    public DnsEdnsRecord? EdnsRecord { get; init; }

    public int TotalLength { get; init; }
    public byte[]? RawBytes { get; init; }

    public override string ToString()
    {
        var type = IsResponse ? "Response" : "Query";
        var proto = IsMdns ? "mDNS" : "DNS";
        var q = Questions.Count > 0 ? Questions[0].Name : "?";
        return $"{proto} {type} id={TransactionId} q={q} answers={Answers.Count}";
    }
}

public sealed record DnsQuestion(string Name, DnsRecordType Type, ushort Class);

/// <summary>Parsed EDNS0 OPT pseudo-RR (RFC 6891).</summary>
public sealed class DnsEdnsRecord
{
    /// <summary>Requestor's UDP payload size (from the OPT RR class field).</summary>
    public ushort UdpPayloadSize { get; init; }

    /// <summary>Extended RCODE (high 8 bits of the OPT TTL field).</summary>
    public byte ExtendedRcode { get; init; }

    /// <summary>EDNS version (second byte of TTL field; must be 0).</summary>
    public byte Version { get; init; }

    /// <summary>DNSSEC OK bit — set when the sender supports DNSSEC records.</summary>
    public bool DoBit { get; init; }

    /// <summary>EDNS options from the OPT RDATA (code + data pairs).</summary>
    public IReadOnlyList<DnsEdnsOption> Options { get; init; } = [];
}

/// <summary>A single EDNS option (code + opaque data).</summary>
public sealed record DnsEdnsOption(ushort Code, byte[] Data);

public sealed class DnsResourceRecord
{
    public string Name { get; init; } = string.Empty;
    public DnsRecordType Type { get; init; }
    public ushort Class { get; init; }
    public uint Ttl { get; init; }
    public byte[] RData { get; init; } = [];

    /// <summary>Decoded RData as a human-readable string (IP address, domain name, etc.).</summary>
    public string? DataString { get; init; }
}
