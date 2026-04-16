namespace IoTSpy.Core.Models;

public class ApiKey
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Display name set by the user at creation time.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>SHA-256 hash (Base64) of the raw plaintext key. Enables O(1) lookup.</summary>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>Space-delimited scope string, e.g. "captures:read scanner:read".</summary>
    public string Scopes { get; set; } = string.Empty;

    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>ID of the user who owns this key (used to resolve role claims).</summary>
    public Guid OwnerId { get; set; }

    public bool IsRevoked { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
