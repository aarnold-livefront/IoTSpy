namespace IoTSpy.Core.Models;

public class CoapProxySettings
{
    public bool Enabled { get; set; }
    public int ListenPort { get; set; } = 5683;
    public string ListenAddress { get; set; } = "0.0.0.0";
    public string? UpstreamHost { get; set; }
    public int UpstreamPort { get; set; } = 5683;
    public bool LogPayloads { get; set; } = true;
}
