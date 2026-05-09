using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IProtoSchemaRepository
{
    Task<IReadOnlyList<ProtoSchema>> GetAllAsync(CancellationToken ct = default);
    Task<ProtoSchema?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProtoSchema> AddAsync(ProtoSchema schema, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
