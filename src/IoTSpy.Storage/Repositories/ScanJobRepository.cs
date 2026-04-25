using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Storage.Repositories;

public class ScanJobRepository(IoTSpyDbContext db) : IScanJobRepository
{
    public async Task<ScanJob> AddAsync(ScanJob job, CancellationToken ct = default)
    {
        db.ScanJobs.Add(job);
        await db.SaveChangesAsync(ct);
        return job;
    }

    public Task<ScanJob?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.ScanJobs
            .AsNoTracking()
            .Include(j => j.Findings)
            .FirstOrDefaultAsync(j => j.Id == id, ct);

    public Task<List<ScanJob>> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct = default) =>
        db.ScanJobs
            .AsNoTracking()
            .Where(j => j.DeviceId == deviceId)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync(ct);

    public Task<List<ScanJob>> GetAllAsync(int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        db.ScanJobs
            .AsNoTracking()
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct = default) =>
        db.ScanJobs.CountAsync(ct);

    public async Task<ScanJob> UpdateAsync(ScanJob job, CancellationToken ct = default)
    {
        db.ScanJobs.Update(job);
        await db.SaveChangesAsync(ct);
        return job;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var job = await db.ScanJobs.FindAsync([id], ct);
        if (job is not null)
        {
            db.ScanJobs.Remove(job);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task AddFindingAsync(ScanFinding finding, CancellationToken ct = default)
    {
        db.ScanFindings.Add(finding);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddFindingsAsync(IEnumerable<ScanFinding> findings, CancellationToken ct = default)
    {
        db.ScanFindings.AddRange(findings);
        await db.SaveChangesAsync(ct);
    }

    public Task<List<ScanFinding>> GetFindingsAsync(Guid scanJobId, CancellationToken ct = default) =>
        db.ScanFindings
            .AsNoTracking()
            .Where(f => f.ScanJobId == scanJobId)
            .OrderBy(f => f.FoundAt)
            .ToListAsync(ct);
}
