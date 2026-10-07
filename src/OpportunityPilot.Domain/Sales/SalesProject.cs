using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Sales;

public enum SalesProjectSource
{
    Upwork,
    Freelancer,
    TenderFeed,
    PublicUrl,
    Manual
}

public enum SalesProjectState
{
    New,
    Shortlisted,
    BidPrepared,
    BidApproved,
    BidPlaced,
    ManualHandoff,
    Dismissed
}

/// <summary>An owner-scoped project or tender found for the sales workspace.</summary>
public sealed class SalesProject : IOwned
{
    public const int MaxExternalIdLength = 200;
    public const int MaxTitleLength = 300;
    public const int MaxBuyerLength = 300;
    public const int MaxDescriptionLength = 8_000;
    public const int MaxUrlLength = 1_000;

    private SalesProject() { }

    public SalesProject(Guid ownerId, SalesProjectSource source, string? externalId, string title, DateTime utcNow)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerId));
        if (!Enum.IsDefined(source)) throw new ArgumentOutOfRangeException(nameof(source));

        Id = Guid.NewGuid();
        OwnerId = ownerId;
        Source = source;
        ExternalId = Guard.Optional(externalId, MaxExternalIdLength, nameof(externalId));
        Title = Guard.Required(title, MaxTitleLength, nameof(title));
        State = SalesProjectState.New;
        EvidenceJson = "[]";
        CreatedAt = utcNow;
        UpdatedAt = utcNow;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public SalesProjectSource Source { get; private set; }
    public string? ExternalId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Buyer { get; private set; }
    public string? Description { get; private set; }
    public string? Url { get; private set; }
    public DateTime? DeadlineUtc { get; private set; }
    public SalesProjectState State { get; private set; }
    public string EvidenceJson { get; private set; } = "[]";
    public int Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void UpdateDetails(string? buyer, string? description, string? url, DateTime? deadlineUtc, string evidenceJson, DateTime utcNow)
    {
        Buyer = Guard.Optional(buyer, MaxBuyerLength, nameof(buyer));
        Description = Guard.Optional(description, MaxDescriptionLength, nameof(description));
        Url = Guard.Optional(url, MaxUrlLength, nameof(url));
        DeadlineUtc = deadlineUtc;
        EvidenceJson = string.IsNullOrWhiteSpace(evidenceJson) ? "[]" : evidenceJson;
        Version++;
        UpdatedAt = utcNow;
    }

    public void ChangeState(SalesProjectState state, DateTime utcNow)
    {
        if (!Enum.IsDefined(state)) throw new ArgumentOutOfRangeException(nameof(state));
        if (State == state) return;
        State = state;
        Version++;
        UpdatedAt = utcNow;
    }

    /// <summary>Stable provider context included in the bid approval hash.</summary>
    public string ApprovalContext() => $"{Source}|{ExternalId ?? string.Empty}|{Url ?? string.Empty}|{EvidenceJson}";
}
