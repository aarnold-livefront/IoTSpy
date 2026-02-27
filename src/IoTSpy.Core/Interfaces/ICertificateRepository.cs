using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface ICertificateRepository
{
    Task<CertificateEntry?> GetRootCaAsync(CancellationToken ct = default);
    Task<CertificateEntry?> GetByHostnameAsync(string hostname, CancellationToken ct = default);
    Task<List<CertificateEntry>> GetAllAsync(CancellationToken ct = default);
    Task<CertificateEntry> SaveAsync(CertificateEntry entry, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
