using IoTSpy.Core.Enums;

namespace IoTSpy.Core.Models;

public class CapturedRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? DeviceId { get; set; }
    public Device? Device { get; set; }

    // Request
    public string Method { get; set; } = string.Empty;
    public string Scheme { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string RequestHeaders { get; set; } = string.Empty;     // JSON-serialized
    public string RequestBody { get; set; } = string.Empty;
    public long RequestBodySize { get; set; }

    // Response
    public int StatusCode { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public string ResponseHeaders { get; set; } = string.Empty;    // JSON-serialized
    public string ResponseBody { get; set; } = string.Empty;
    public long ResponseBodySize { get; set; }

    // TLS
    public bool IsTls { get; set; }
    public string TlsVersion { get; set; } = string.Empty;
    public string TlsCipherSuite { get; set; } = string.Empty;

    // Meta
    public InterceptionProtocol Protocol { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public long DurationMs { get; set; }
    public string ClientIp { get; set; } = string.Empty;
    public bool IsModified { get; set; }
    public string Notes { get; set; } = string.Empty;
}
