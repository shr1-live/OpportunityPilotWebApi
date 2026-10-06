using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Auth;

/// <summary>
/// A provider-independent guest login. Only the SHA-256 hash of the opaque browser token is persisted.
/// Durable databases keep the session valid across API restarts; demo-mode data and sessions reset together.
/// </summary>
public sealed class GuestSession : IOwned
{
    private GuestSession() { }

    public GuestSession(Guid ownerId, string tokenHash, DateTime createdAt, DateTime expiresAt)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerId));
        if (tokenHash is not { Length: 64 }) throw new ArgumentException("Token hash must be 64 hex characters.", nameof(tokenHash));
        if (expiresAt <= createdAt) throw new ArgumentOutOfRangeException(nameof(expiresAt));

        Id = Guid.NewGuid();
        OwnerId = ownerId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
}
