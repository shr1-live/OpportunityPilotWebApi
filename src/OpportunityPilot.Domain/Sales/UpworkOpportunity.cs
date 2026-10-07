using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Sales;

public enum UpworkOpportunityState
{
    Saved,
    Shortlisted,
    Dismissed,
    Promoted
}

public enum UpworkBudgetType
{
    Unknown,
    FixedPrice,
    Hourly
}

/// <summary>A human-reviewed Upwork research result; it never represents a submitted proposal.</summary>
public sealed class UpworkOpportunity : IOwned
{
    public const int MaxProviderJobIdLength = 200;
    public const int MaxTitleLength = 300;
    public const int MaxSummaryLength = 8_000;
    public const int MaxUrlLength = 1_000;
    public const int MaxLocationLength = 200;
    public const int MaxExperienceLevelLength = 100;
    public const int MaxCurrencyLength = 3;

    private UpworkOpportunity() { }

    public UpworkOpportunity(Guid ownerId, string providerJobId, string title, string url, DateTime observedAt, DateTime utcNow)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerId));
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        ProviderJobId = Guard.Required(providerJobId, MaxProviderJobIdLength, nameof(providerJobId));
        Title = Guard.Required(title, MaxTitleLength, nameof(title));
        Url = Guard.Required(url, MaxUrlLength, nameof(url));
        ObservedAt = observedAt;
        State = UpworkOpportunityState.Saved;
        EvidenceJson = "{}";
        CreatedAt = utcNow;
        UpdatedAt = utcNow;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string ProviderJobId { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Url { get; private set; } = string.Empty;
    public string? Summary { get; private set; }
    public string? Location { get; private set; }
    public UpworkBudgetType BudgetType { get; private set; }
    public decimal? BudgetMin { get; private set; }
    public decimal? BudgetMax { get; private set; }
    public string? Currency { get; private set; }
    public string? ExperienceLevel { get; private set; }
    public int? ConnectsRequired { get; private set; }
    public int? AvailableConnectsAtReview { get; private set; }
    public bool? PaymentVerified { get; private set; }
    public DateTime? PostedAt { get; private set; }
    public DateTime ObservedAt { get; private set; }
    public string EvidenceJson { get; private set; } = "{}";
    public UpworkOpportunityState State { get; private set; }
    public Guid? SalesProjectId { get; private set; }
    public int Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void UpdateResearch(string? summary, string? location, UpworkBudgetType budgetType, decimal? budgetMin,
        decimal? budgetMax, string? currency, string? experienceLevel, int? connectsRequired,
        int? availableConnectsAtReview, bool? paymentVerified, DateTime? postedAt, DateTime observedAt,
        string evidenceJson, DateTime utcNow)
    {
        if (!Enum.IsDefined(budgetType)) throw new ArgumentOutOfRangeException(nameof(budgetType));
        if (budgetMin < 0 || budgetMax < 0 || (budgetMin is not null && budgetMax is not null && budgetMin > budgetMax))
            throw new ArgumentException("Budget values are invalid.", nameof(budgetMin));
        if (connectsRequired < 0 || availableConnectsAtReview < 0)
            throw new ArgumentException("Connects values cannot be negative.", nameof(connectsRequired));
        Summary = Guard.Optional(summary, MaxSummaryLength, nameof(summary));
        Location = Guard.Optional(location, MaxLocationLength, nameof(location));
        BudgetType = budgetType;
        BudgetMin = budgetMin;
        BudgetMax = budgetMax;
        Currency = Guard.Optional(currency?.ToUpperInvariant(), MaxCurrencyLength, nameof(currency));
        ExperienceLevel = Guard.Optional(experienceLevel, MaxExperienceLevelLength, nameof(experienceLevel));
        ConnectsRequired = connectsRequired;
        AvailableConnectsAtReview = availableConnectsAtReview;
        PaymentVerified = paymentVerified;
        PostedAt = postedAt;
        ObservedAt = observedAt;
        EvidenceJson = string.IsNullOrWhiteSpace(evidenceJson) ? "{}" : evidenceJson;
        Version++;
        UpdatedAt = utcNow;
    }

    public void ChangeState(UpworkOpportunityState state, int expectedVersion, DateTime utcNow)
    {
        if (Version != expectedVersion) throw new InvalidOperationException($"Opportunity was changed elsewhere (now version {Version}). Reload before saving.");
        if (!Enum.IsDefined(state) || state == UpworkOpportunityState.Promoted)
            throw new ArgumentOutOfRangeException(nameof(state));
        State = state;
        Version++;
        UpdatedAt = utcNow;
    }

    public void MarkPromoted(Guid salesProjectId, DateTime utcNow)
    {
        if (salesProjectId == Guid.Empty) throw new ArgumentException("Sales project is required.", nameof(salesProjectId));
        SalesProjectId = salesProjectId;
        State = UpworkOpportunityState.Promoted;
        Version++;
        UpdatedAt = utcNow;
    }
}
