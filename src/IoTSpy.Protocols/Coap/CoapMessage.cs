using System.Text;

namespace IoTSpy.Protocols.Coap;

/// <summary>
/// Represents a decoded CoAP message per RFC 7252.
/// </summary>
public sealed class CoapMessage
{
    /// <summary>CoAP version (always 1 for RFC 7252).</summary>
    public byte Version { get; init; }

    /// <summary>Message type: CON, NON, ACK, RST.</summary>
    public CoapMessageType Type { get; init; }

    /// <summary>Token length (0-8 bytes).</summary>
    public byte TokenLength { get; init; }

    /// <summary>
    /// Code field encoded as a single byte: class (upper 3 bits) | detail (lower 5 bits).
    /// </summary>
    public byte Code { get; init; }

    /// <summary>Code formatted as "class.detail" (e.g. "0.01" = GET).</summary>
    public string CodeString => CoapCode.Format(Code);

    /// <summary>Human-readable code name.</summary>
    public string CodeName => CoapCode.GetName(Code);

    /// <summary>True if the code class is 0 (request method).</summary>
    public bool IsRequest => (Code >> 5) == 0 && Code != 0;

    /// <summary>True if the code class is 2-5 (response).</summary>
    public bool IsResponse => (Code >> 5) >= 2;

    /// <summary>16-bit message ID for matching CON/ACK pairs.</summary>
    public ushort MessageId { get; init; }

    /// <summary>Token bytes (0-8 bytes) for request/response matching.</summary>
    public byte[] Token { get; init; } = [];

    /// <summary>Decoded CoAP options.</summary>
    public IReadOnlyList<CoapOption> Options { get; init; } = [];

    /// <summary>Payload bytes (after the 0xFF marker).</summary>
    public byte[]? Payload { get; init; }

    /// <summary>Payload decoded as UTF-8 (best-effort). Null when payload is null.</summary>
    public string? PayloadString => Payload is null ? null : Encoding.UTF8.GetString(Payload);

    /// <summary>Total decoded length in bytes.</summary>
    public int TotalLength { get; init; }

    /// <summary>Raw bytes of the entire message.</summary>
    public byte[]? RawBytes { get; init; }

    // ── Convenience accessors derived from options ───────────────────────

    /// <summary>Reconstructed URI path from Uri-Path options.</summary>
    public string UriPath => string.Join("/",
        Options.Where(o => o.Number == CoapOptionNumber.UriPath)
               .Select(o => o.StringValue));

    /// <summary>Uri-Host option value.</summary>
    public string? UriHost =>
        Options.FirstOrDefault(o => o.Number == CoapOptionNumber.UriHost)?.StringValue;

    /// <summary>Uri-Port option value.</summary>
    public ushort? UriPort =>
        Options.FirstOrDefault(o => o.Number == CoapOptionNumber.UriPort) is { } opt
            ? opt.UIntValue is <= ushort.MaxValue ? (ushort)opt.UIntValue : null
            : null;

    /// <summary>Reconstructed query from Uri-Query options.</summary>
    public string? UriQuery
    {
        get
        {
            var queries = Options
                .Where(o => o.Number == CoapOptionNumber.UriQuery)
                .Select(o => o.StringValue)
                .ToList();
            return queries.Count > 0 ? string.Join("&", queries) : null;
        }
    }

    /// <summary>Content-Format option value.</summary>
    public uint? ContentFormat =>
        Options.FirstOrDefault(o => o.Number == CoapOptionNumber.ContentFormat)?.UIntValue;

    public override string ToString()
    {
        var path = UriPath;
        return IsRequest
            ? $"CoAP {CodeName} /{path} type={Type} mid={MessageId}"
            : $"CoAP {CodeString} ({CodeName}) type={Type} mid={MessageId}";
    }
}

/// <summary>
/// A single CoAP option.
/// </summary>
public sealed class CoapOption
{
    public CoapOptionNumber Number { get; init; }
    public byte[] Value { get; init; } = [];

    /// <summary>Interpret the option value as a UTF-8 string.</summary>
    public string StringValue => Encoding.UTF8.GetString(Value);

    /// <summary>Interpret the option value as an unsigned integer (0-4 bytes, big-endian).</summary>
    public uint UIntValue
    {
        get
        {
            uint result = 0;
            foreach (var b in Value)
                result = (result << 8) | b;
            return result;
        }
    }

    public override string ToString() => $"{Number}={StringValue}";
}
