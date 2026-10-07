using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Outreach;

public sealed class Suppression : IOwned
{
    public const int MaxRecipientLength = 320;
    public const int MaxReasonLength = 200;

    private Suppression() { }

    public Suppression(Guid ownerId, string recipient, string? reason, DateTime utcNow)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerId));
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        NormalizedRecipient = Normalize(recipient);
        Reason = Guard.Truncate(reason, MaxReasonLength);
        CreatedAt = utcNow;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string NormalizedRecipient { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    public static string Normalize(string? recipient) =>
        Guard.Required(recipient, MaxRecipientLength, nameof(recipient)).ToLowerInvariant();
}
