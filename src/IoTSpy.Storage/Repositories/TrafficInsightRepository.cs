using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Storage.Repositories;

public class TrafficInsightRepository(IoTSpyDbContext db) : ITrafficInsightRepository
{
    public Task<TrafficInsight?> GetByCaptureIdAsync(Guid captureId, CancellationToken ct = default) =>
        db.TrafficInsights.AsNoTracking().FirstOrDefaultAsync(i => i.CaptureId == captureId, ct);

    public Task<List<TrafficInsight>> GetTriageQueueAsync(
        int page, int pageSize, bool unreviewedOnly, CancellationToken ct = default)
    {
        var q = unreviewedOnly
            ? db.TrafficInsights.Where(i => !i.IsReviewed)
            : db.TrafficInsights.AsQueryable();

        return q.OrderByDescending(i => i.RiskScore)
                .ThenByDescending(i => i.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync(ct);
    }

    public Task<int> CountTriageQueueAsync(bool unreviewedOnly, CancellationToken ct = default)
    {
        var q = unreviewedOnly
            ? db.TrafficInsights.Where(i => !i.IsReviewed)
            : db.TrafficInsights.AsQueryable();
        return q.CountAsync(ct);
    }

    public async Task UpsertAsync(TrafficInsight insight, CancellationToken ct = default)
    {
        var existing = await db.TrafficInsights
            .FirstOrDefaultAsync(i => i.CaptureId == insight.CaptureId, ct);

        if (existing is null)
        {
            db.TrafficInsights.Add(insight);
        }
        else
        {
            existing.TagsJson = insight.TagsJson;
            existing.ConfidenceJson = insight.ConfidenceJson;
            existing.RiskScore = insight.RiskScore;
            existing.ModelVersion = insight.ModelVersion;
            existing.Source = insight.Source;
            existing.CreatedAt = insight.CreatedAt;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task MarkReviewedAsync(Guid id, Guid userId, bool dismissed, string? note, CancellationToken ct = default)
    {
        var insight = await db.TrafficInsights.FindAsync([id], ct);
        if (insight is null) return;

        insight.IsReviewed = true;
        insight.IsDismissed = dismissed;
        insight.ReviewNote = note;
        insight.ReviewedByUserId = userId;
        insight.ReviewedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    public Task<List<TrafficInsight>> GetByCaptureIdsAsync(
        IEnumerable<Guid> captureIds, CancellationToken ct = default)
    {
        var ids = captureIds.ToList();
        return db.TrafficInsights
            .Where(i => ids.Contains(i.CaptureId))
            .AsNoTracking()
            .ToListAsync(ct);
    }
}
