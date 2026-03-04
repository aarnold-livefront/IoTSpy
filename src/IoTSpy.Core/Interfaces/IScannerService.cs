using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IScannerService
{
    Task<ScanJob> StartScanAsync(ScanJob job, CancellationToken ct = default);
    Task CancelScanAsync(Guid scanJobId);
    bool IsScanRunning(Guid scanJobId);
}
