using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Storage.Repositories;

public class CertificateRepository(IoTSpyDbContext db) : ICertificateRepository
{
    public Task<CertificateEntry?> GetRootCaAsync(CancellationToken ct = default) =>
        db.Certificates.FirstOrDefaultAsync(c => c.IsRootCa, ct);

    public Task<CertificateEntry?> GetByHostnameAsync(string hostname, CancellationToken ct = default) =>
        db.Certificates.FirstOrDefaultAsync(c => !c.IsRootCa && c.CommonName == hostname, ct);

    public Task<List<CertificateEntry>> GetAllAsync(CancellationToken ct = default) =>
        db.Certificates.OrderByDescending(c => c.CreatedAt).ToListAsync(ct);

    public async Task<CertificateEntry> SaveAsync(CertificateEntry entry, CancellationToken ct = default)
    {
        var existing = await db.Certificates.FindAsync([entry.Id], ct);
        if (existing is null)
            db.Certificates.Add(entry);
        else
            db.Entry(existing).CurrentValues.SetValues(entry);
        await db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var cert = await db.Certificates.FindAsync([id], ct);
        if (cert is not null)
        {
            db.Certificates.Remove(cert);
            await db.SaveChangesAsync(ct);
        }
    }
}
