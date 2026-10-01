using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Agents;

/// <summary>
/// A personal key the local desktop agent presents to report results. Only the SHA-256 of the key is stored;
/// the plaintext is shown once at creation. <see cref="Prefix"/> lets the user tell keys apart.
/// </summary>
public class AgentKey : IOwned
{
    private AgentKey() { }

    public AgentKey(Guid ownerId, string name, string keyHash, string prefix, DateTime utcNow)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
        if (keyHash is not { Length: 64 }) throw new ArgumentException("Key hash must be 64 hex characters.", nameof(keyHash));
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        Name = name.Trim();
        KeyHash = keyHash;
        Prefix = prefix;
        CreatedAt = utcNow;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string KeyHash { get; private set; } = string.Empty;
    public string Prefix { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastUsedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    /// <summary>Idempotent: the first revocation time is kept.</summary>
    public void Revoke(DateTime utcNow) => RevokedAt ??= utcNow;

    public void MarkUsed(DateTime utcNow) => LastUsedAt = utcNow;
}
