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
