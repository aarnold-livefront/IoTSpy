namespace IoTSpy.Core.Interfaces;

public enum AlertSeverity { Info, Warning, Critical }

public interface IAlertingService
{
    Task SendAlertAsync(string title, string body, AlertSeverity severity, CancellationToken ct = default);
}
