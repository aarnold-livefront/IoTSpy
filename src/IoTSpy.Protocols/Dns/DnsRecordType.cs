namespace IoTSpy.Protocols.Dns;

/// <summary>
/// Common DNS resource record types (RFC 1035, 3596, 6762, etc.).
/// </summary>
public enum DnsRecordType : ushort
{
    A = 1,
    NS = 2,
    CNAME = 5,
    SOA = 6,
    PTR = 12,
    MX = 15,
    TXT = 16,
    AAAA = 28,
    SRV = 33,
    ANY = 255
}
