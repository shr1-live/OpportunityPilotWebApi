using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Staffing;

public enum StaffingDealActivityType
{
    Created,
    StageChanged,
    DetailsChanged,
    NoteAdded,
    ManualActionConfirmed,
    ProviderReceiptRecorded
}

/// <summary>An immutable, safe audit fact about a staffing deal.</summary>
public sealed class StaffingDealActivity : IOwned
{
    public const int MaxDetailLength = 2_000;

    private StaffingDealActivity() { }

    public StaffingDealActivity(Guid ownerId, Guid dealId, StaffingDealActivityType type, string detail, DateTime occurredAt)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerId));
        if (dealId == Guid.Empty) throw new ArgumentException("Deal is required.", nameof(dealId));
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        DealId = dealId;
        Type = type;
        Detail = Guard.Required(detail, MaxDetailLength, nameof(detail));
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid DealId { get; private set; }
    public StaffingDealActivityType Type { get; private set; }
    public string Detail { get; private set; } = string.Empty;
    public DateTime OccurredAt { get; private set; }
}
