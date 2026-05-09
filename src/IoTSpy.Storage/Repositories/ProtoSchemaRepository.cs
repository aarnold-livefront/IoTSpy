using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Storage.Repositories;

public class ProtoSchemaRepository(IoTSpyDbContext db) : IProtoSchemaRepository
{
    public async Task<IReadOnlyList<ProtoSchema>> GetAllAsync(CancellationToken ct = default)
        => await db.ProtoSchemas.AsNoTracking().OrderByDescending(s => s.CreatedAt).ToListAsync(ct);

    public async Task<ProtoSchema?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.ProtoSchemas.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<ProtoSchema> AddAsync(ProtoSchema schema, CancellationToken ct = default)
    {
        db.ProtoSchemas.Add(schema);
        await db.SaveChangesAsync(ct);
        return schema;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await db.ProtoSchemas.Where(s => s.Id == id).ExecuteDeleteAsync(ct);
    }
}
