using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IApiKeyRepository
{
    Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Looks up a key by its SHA-256 hash.</summary>
    Task<ApiKey?> GetByHashAsync(string hash, CancellationToken ct = default);

    Task<List<ApiKey>> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default);
    Task<List<ApiKey>> GetAllAsync(CancellationToken ct = default);
    Task<ApiKey> CreateAsync(ApiKey key, CancellationToken ct = default);
    Task<ApiKey> UpdateAsync(ApiKey key, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
