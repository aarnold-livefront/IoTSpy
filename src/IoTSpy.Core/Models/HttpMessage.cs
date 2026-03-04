namespace IoTSpy.Core.Models;

/// <summary>
/// Mutable representation of an HTTP request or response flowing through the proxy,
/// used by the manipulation pipeline to apply rules and scripts before forwarding.
/// </summary>
public class HttpMessage
{
    // Request fields
    public string Method { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string Scheme { get; set; } = string.Empty;

    // Request line / Status line (raw first line)
    public string RequestLine { get; set; } = string.Empty;
    public string StatusLine { get; set; } = string.Empty;
    public int StatusCode { get; set; }

    // Headers + body (mutable)
    public string RequestHeaders { get; set; } = string.Empty;
    public string RequestBody { get; set; } = string.Empty;
    public string ResponseHeaders { get; set; } = string.Empty;
    public string ResponseBody { get; set; } = string.Empty;

    /// <summary>
    /// Set to true by the manipulation pipeline when any rule or script modified this message.
    /// </summary>
    public bool WasModified { get; set; }
}
