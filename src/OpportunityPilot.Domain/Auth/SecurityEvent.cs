using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Auth;

public enum SecurityEventType
{
    GuestSessionIssued,
    AgentKeyCreated,
    AgentKeyRevoked,
    AgentKeyScopesChanged,
    AccountDataExported,
    AccountDataDeleted
}

/// <summary>
/// An append-only, owner-scoped record of a security-relevant action. It holds no secrets and no personal data beyond
/// the owner id: the detail names what happened (e.g. the key's prefix), never a key, token or address.
/// </summary>
public sealed class SecurityEvent : IOwned
{
    public const int MaxDetailLength = 500;

    private SecurityEvent() { }

    public SecurityEvent(Guid ownerId, SecurityEventType type, string? detail, DateTime utcNow)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerId));
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        Type = type;
        Detail = Guard.TruncateOptional(detail, MaxDetailLength);
        OccurredAt = utcNow;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public SecurityEventType Type { get; private set; }
    public string? Detail { get; private set; }
    public DateTime OccurredAt { get; private set; }
}
