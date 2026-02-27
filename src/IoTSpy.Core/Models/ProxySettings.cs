using IoTSpy.Core.Enums;

namespace IoTSpy.Core.Models;

public class ProxySettings
{
    public int Id { get; set; } = 1;
    public int ProxyPort { get; set; } = 8888;
    public ProxyMode Mode { get; set; } = ProxyMode.ExplicitProxy;
    public bool IsRunning { get; set; }
    public bool CaptureTls { get; set; } = true;
    public bool CaptureRequestBodies { get; set; } = true;
    public bool CaptureResponseBodies { get; set; } = true;
    public int MaxBodySizeKb { get; set; } = 1024;
    public string ListenAddress { get; set; } = "127.0.0.1";
    public string PasswordHash { get; set; } = string.Empty;
}
