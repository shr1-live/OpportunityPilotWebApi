using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Staffing;

public enum RateCardStatus { Draft, Active, Retired }
public enum ProposalState { Draft, Approved, Sent, Accepted, Rejected }

/// <summary>A named, versioned price list (roles × seniority × rate). Editing an active card creates a new version.</summary>
public sealed class StaffingRateCard : IOwned
{
    public const int MaxNameLength = 200;
    public const int MaxLinesLength = 20_000;
    public const int MaxTermsLength = 4_000;

    private StaffingRateCard() { }

    public StaffingRateCard(Guid ownerId, string name, string currency, string linesJson, string? terms, DateOnly? validUntil, DateTime utcNow)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerId));
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        CardVersion = 1;
        Status = RateCardStatus.Draft;
        CreatedAt = utcNow;
        Set(name, currency, linesJson, terms, validUntil, utcNow);
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Currency { get; private set; } = string.Empty;
    /// <summary>JSON array of { role, seniority, unit, rate }.</summary>
    public string LinesJson { get; private set; } = "[]";
    public string? Terms { get; private set; }
    public DateOnly? ValidUntil { get; private set; }
    /// <summary>Business version shown to people; rises on every edit after activation.</summary>
    public int CardVersion { get; private set; }
    public RateCardStatus Status { get; private set; }
    public int Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Set(string name, string currency, string linesJson, string? terms, DateOnly? validUntil, DateTime utcNow)
    {
        if (Status == RateCardStatus.Retired) throw new InvalidOperationException("A retired rate card cannot be edited.");
        Name = Guard.Required(name, MaxNameLength, nameof(name));
        var c = Guard.Required(currency, 3, nameof(currency)).ToUpperInvariant();
        if (c.Length != 3 || !c.All(char.IsAsciiLetterUpper)) throw new ArgumentException("Currency is a 3-letter code.", nameof(currency));
        Currency = c;
        LinesJson = Guard.Required(linesJson, MaxLinesLength, nameof(linesJson));
        Terms = Guard.Optional(terms, MaxTermsLength, nameof(terms));
        ValidUntil = validUntil;
        if (Status == RateCardStatus.Active) CardVersion++;
        Version++;
        UpdatedAt = utcNow;
    }

    public void SetStatus(RateCardStatus status, DateTime utcNow)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if (Status == RateCardStatus.Retired && status != RateCardStatus.Retired) throw new InvalidOperationException("A retired rate card stays retired.");
        Status = status;
        Version++;
        UpdatedAt = utcNow;
    }
}

/// <summary>
/// A commercial proposal for a deal: roles and rates copied from a rate card version, plus terms and validity. Any edit
/// returns it to Draft, so approval always covers the exact content that goes out.
/// </summary>
public sealed class StaffingProposal : IOwned
{
    public const int MaxTitleLength = 300;
    public const int MaxBodyLength = 20_000;

    private StaffingProposal() { }

    public StaffingProposal(Guid ownerId, Guid dealId, Guid rateCardId, int rateCardVersion, string title, string currency,
        string linesJson, string? terms, string? body, DateOnly? validUntil, DateTime utcNow)
    {
        if (ownerId == Guid.Empty || dealId == Guid.Empty || rateCardId == Guid.Empty)
            throw new ArgumentException("Owner, deal and rate card are required.");
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        DealId = dealId;
        RateCardId = rateCardId;
        RateCardVersion = rateCardVersion;
        CreatedAt = utcNow;
        Edit(title, currency, linesJson, terms, body, validUntil, utcNow);
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid DealId { get; private set; }
    public Guid RateCardId { get; private set; }
    public int RateCardVersion { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Currency { get; private set; } = string.Empty;
    public string LinesJson { get; private set; } = "[]";
    public string? Terms { get; private set; }
    public string? Body { get; private set; }
    public DateOnly? ValidUntil { get; private set; }
    public ProposalState State { get; private set; }
    public int? ApprovedVersion { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public string? Receipt { get; private set; }
    public DateTime? SentAt { get; private set; }
    public int Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Edit(string title, string currency, string linesJson, string? terms, string? body, DateOnly? validUntil, DateTime utcNow)
    {
        if (State is not (ProposalState.Draft or ProposalState.Approved))
            throw new InvalidOperationException("A sent proposal cannot be edited; create a new one.");
        Title = Guard.Required(title, MaxTitleLength, nameof(title));
        Currency = Guard.Required(currency, 3, nameof(currency)).ToUpperInvariant();
        LinesJson = Guard.Required(linesJson, StaffingRateCard.MaxLinesLength, nameof(linesJson));
        Terms = Guard.Optional(terms, StaffingRateCard.MaxTermsLength, nameof(terms));
        Body = Guard.Optional(body, MaxBodyLength, nameof(body));
        ValidUntil = validUntil;
        State = ProposalState.Draft;
        ApprovedVersion = null;
        ApprovedAt = null;
        Version++;
        UpdatedAt = utcNow;
    }

    public void Approve(int expectedVersion, DateTime utcNow)
    {
        if (State != ProposalState.Draft) throw new InvalidOperationException("Only a draft proposal can be approved.");
        if (expectedVersion != Version) throw new InvalidOperationException($"The proposal changed (now version {Version}). Review it again.");
        if (Body is not null && Body.Contains('[') && Body.Contains(']'))
            throw new InvalidOperationException("Replace every [placeholder] before approving this proposal.");
        State = ProposalState.Approved;
        Version++;
        ApprovedVersion = Version;
        ApprovedAt = utcNow;
        UpdatedAt = utcNow;
    }

    public void MarkSent(int expectedVersion, string receipt, DateTime utcNow)
    {
        if (State != ProposalState.Approved || expectedVersion != Version || ApprovedVersion != Version)
            throw new InvalidOperationException("Only the approved current version can be marked as sent.");
        Receipt = Guard.Required(receipt, 1_000, nameof(receipt));
        State = ProposalState.Sent;
        SentAt = utcNow;
        Version++;
        UpdatedAt = utcNow;
    }

    public void RecordClientAnswer(bool accepted, DateTime utcNow)
    {
        if (State != ProposalState.Sent) throw new InvalidOperationException("Record the client's answer after the proposal is sent.");
        State = accepted ? ProposalState.Accepted : ProposalState.Rejected;
        Version++;
        UpdatedAt = utcNow;
    }
}
