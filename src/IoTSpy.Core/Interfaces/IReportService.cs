namespace IoTSpy.Core.Interfaces;

public interface IReportService
{
    Task<byte[]> GenerateHtmlReportAsync(Guid deviceId, CancellationToken ct = default);
    Task<byte[]> GeneratePdfReportAsync(Guid deviceId, CancellationToken ct = default);
}
