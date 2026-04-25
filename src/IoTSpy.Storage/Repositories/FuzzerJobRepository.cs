using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Storage.Repositories;

public class FuzzerJobRepository(IoTSpyDbContext db) : IFuzzerJobRepository
{
    public async Task<FuzzerJob> AddAsync(FuzzerJob job, CancellationToken ct = default)
    {
        db.FuzzerJobs.Add(job);
        await db.SaveChangesAsync(ct);
        return job;
    }

    public Task<FuzzerJob?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.FuzzerJobs
            .AsNoTracking()
            .Include(j => j.Results)
            .FirstOrDefaultAsync(j => j.Id == id, ct);

    public Task<List<FuzzerJob>> GetAllAsync(int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        db.FuzzerJobs
            .AsNoTracking()
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct = default) =>
        db.FuzzerJobs.CountAsync(ct);

    public async Task<FuzzerJob> UpdateAsync(FuzzerJob job, CancellationToken ct = default)
    {
        db.FuzzerJobs.Update(job);
        await db.SaveChangesAsync(ct);
        return job;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var job = await db.FuzzerJobs.FindAsync([id], ct);
        if (job is not null)
        {
            db.FuzzerJobs.Remove(job);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task AddResultAsync(FuzzerResult result, CancellationToken ct = default)
    {
        db.FuzzerResults.Add(result);
        await db.SaveChangesAsync(ct);
    }

    public Task<List<FuzzerResult>> GetResultsAsync(Guid jobId, CancellationToken ct = default) =>
        db.FuzzerResults
            .AsNoTracking()
            .Where(r => r.FuzzerJobId == jobId)
            .OrderBy(r => r.MutationIndex)
            .ToListAsync(ct);
}
