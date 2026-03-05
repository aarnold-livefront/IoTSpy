namespace IoTSpy.Protocols.Coap;

/// <summary>
/// CoAP option numbers per RFC 7252 §5.10 and common extensions.
/// </summary>
public enum CoapOptionNumber : ushort
{
    IfMatch = 1,
    UriHost = 3,
    ETag = 4,
    IfNoneMatch = 5,
    Observe = 6,       // RFC 7641
    UriPort = 7,
    LocationPath = 8,
    UriPath = 11,
    ContentFormat = 12,
    MaxAge = 14,
    UriQuery = 15,
    Accept = 17,
    LocationQuery = 20,
    Block2 = 23,       // RFC 7959
    Block1 = 27,       // RFC 7959
    Size2 = 28,        // RFC 7959
    ProxyUri = 35,
    ProxyScheme = 39,
    Size1 = 60
}
