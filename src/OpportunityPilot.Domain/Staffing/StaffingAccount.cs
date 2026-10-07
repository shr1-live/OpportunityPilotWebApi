using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Staffing;

public enum StaffingAccountSource
{
    Manual,
    Import,
    PublicWeb,
    SalesIntelligence,
    Upwork,
    Freelancer,
    Tender,
    Referral
}

/// <summary>A prospective or active client company whose identity remains attributable to its source.</summary>
public sealed class StaffingAccount : IOwned
{
    public const int MaxNameLength = 300;
    public const int MaxDomainLength = 253;
    public const int MaxIndustryLength = 200;
    public const int MaxLocationLength = 300;
    public const int MaxSourceReferenceLength = 1_000;

    private StaffingAccount() { }

    public StaffingAccount(Guid ownerId, string name, StaffingAccountSource source, DateTime utcNow)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerId));
        if (!Enum.IsDefined(source)) throw new ArgumentOutOfRangeException(nameof(source));
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        Name = Guard.Required(name, MaxNameLength, nameof(name));
        Source = source;
        CreatedAt = utcNow;
        UpdatedAt = utcNow;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Domain { get; private set; }
    public string? Industry { get; private set; }
    public string? Location { get; private set; }
    public StaffingAccountSource Source { get; private set; }
    public string? SourceReference { get; private set; }
    public int Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Update(string name, string? domain, string? industry, string? location, string? sourceReference, DateTime utcNow)
    {
        Name = Guard.Required(name, MaxNameLength, nameof(name));
        Domain = Guard.Optional(domain, MaxDomainLength, nameof(domain));
        Industry = Guard.Optional(industry, MaxIndustryLength, nameof(industry));
        Location = Guard.Optional(location, MaxLocationLength, nameof(location));
        SourceReference = Guard.Optional(sourceReference, MaxSourceReferenceLength, nameof(sourceReference));
        Version++;
        UpdatedAt = utcNow;
    }
}
