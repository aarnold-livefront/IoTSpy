namespace IoTSpy.Protocols.Coap;

/// <summary>
/// Well-known CoAP request/response codes per RFC 7252 §5.
/// The code is encoded as c.dd where c is the class (0-7) and dd is the detail (0-31).
/// </summary>
public static class CoapCode
{
    // ── Request methods (class 0) ────────────────────────────────────────
    public const byte Empty = 0x00;       // 0.00
    public const byte Get = 0x01;         // 0.01
    public const byte Post = 0x02;        // 0.02
    public const byte Put = 0x03;         // 0.03
    public const byte Delete = 0x04;      // 0.04

    // ── Success responses (class 2) ──────────────────────────────────────
    public const byte Created = 0x41;     // 2.01
    public const byte Deleted = 0x42;     // 2.02
    public const byte Valid = 0x43;       // 2.03
    public const byte Changed = 0x44;     // 2.04
    public const byte Content = 0x45;     // 2.05

    // ── Client errors (class 4) ──────────────────────────────────────────
    public const byte BadRequest = 0x80;        // 4.00
    public const byte Unauthorized = 0x81;      // 4.01
    public const byte BadOption = 0x82;         // 4.02
    public const byte Forbidden = 0x83;         // 4.03
    public const byte NotFound = 0x84;          // 4.04
    public const byte MethodNotAllowed = 0x85;  // 4.05

    // ── Server errors (class 5) ──────────────────────────────────────────
    public const byte InternalServerError = 0xA0;  // 5.00
    public const byte NotImplemented = 0xA1;       // 5.01
    public const byte BadGateway = 0xA2;           // 5.02
    public const byte ServiceUnavailable = 0xA3;   // 5.03
    public const byte GatewayTimeout = 0xA4;       // 5.04

    /// <summary>
    /// Formats a CoAP code byte as "class.detail" (e.g. "2.05").
    /// </summary>
    public static string Format(byte code) =>
        $"{code >> 5}.{(code & 0x1F):D2}";

    /// <summary>
    /// Returns a human-readable name for the code, or the numeric string.
    /// </summary>
    public static string GetName(byte code) => code switch
    {
        Empty => "Empty",
        Get => "GET",
        Post => "POST",
        Put => "PUT",
        Delete => "DELETE",
        Created => "Created",
        Deleted => "Deleted",
        Valid => "Valid",
        Changed => "Changed",
        Content => "Content",
        BadRequest => "Bad Request",
        Unauthorized => "Unauthorized",
        BadOption => "Bad Option",
        Forbidden => "Forbidden",
        NotFound => "Not Found",
        MethodNotAllowed => "Method Not Allowed",
        InternalServerError => "Internal Server Error",
        NotImplemented => "Not Implemented",
        BadGateway => "Bad Gateway",
        ServiceUnavailable => "Service Unavailable",
        GatewayTimeout => "Gateway Timeout",
        _ => Format(code)
    };
}
