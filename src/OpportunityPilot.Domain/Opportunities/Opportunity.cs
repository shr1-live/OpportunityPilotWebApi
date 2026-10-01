using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Opportunities;

/// <summary>
/// A scored candidate in one campaign, unique per (campaign, <see cref="DedupeKey"/>). Research may re-score it any
/// number of times, but only the user (or a confirmed agent report) moves <see cref="Status"/>: a rerun never
/// resets a Shortlisted or Applied opportunity.
/// </summary>
public class Opportunity : IOwned
{
    public const int MaxDedupeKeyLength = 400;
    public const int MaxTitleLength = 300;
    public const int MaxOrganizationLength = 300;
    public const int MaxLocationLength = 200;
    public const int MaxUrlLength = 1000;
    public const int MaxExternalIdLength = 100;
    public const int MaxDescriptionLength = 4000;
    public const int MaxOutcomeReasonLength = 500;

    private Opportunity() { }

    public Opportunity(Guid ownerId, Guid campaignId, OpportunityMode mode, string dedupeKey, DateTime utcNow)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerId));
        if (campaignId == Guid.Empty) throw new ArgumentException("Campaign is required.", nameof(campaignId));
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        CampaignId = campaignId;
        Mode = mode;
        DedupeKey = Guard.Required(dedupeKey, MaxDedupeKeyLength, nameof(dedupeKey));
        Status = OpportunityStatus.New;
        CreatedAt = utcNow;
        UpdatedAt = utcNow;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid CampaignId { get; private set; }
    public OpportunityMode Mode { get; private set; }
    public string DedupeKey { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;

    /// <summary>Empty when the source did not say (e.g. a pasted posting with no "Company:" line).</summary>
    public string Organization { get; private set; } = string.Empty;

    public string? Location { get; private set; }
    public string? Url { get; private set; }
    public string? ApplyUrl { get; private set; }
    public JobPlatform? Platform { get; private set; }
    public string? ExternalId { get; private set; }
    public string? Description { get; private set; }
    public int Score { get; private set; }
    public int Coverage { get; private set; }
    public FilterOutcome Outcome { get; private set; }
    public string? OutcomeReason { get; private set; }
    public OpportunityStatus Status { get; private set; }
    public string BreakdownJson { get; private set; } = "[]";
    public string FactsJson { get; private set; } = "[]";
    public string GapsJson { get; private set; } = "[]";

    /// <summary>Denormalised from <see cref="GapsJson"/> so list queries need not parse JSON.</summary>
    public int GapsCount { get; private set; }

    public Guid? LastResearchJobId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public int Version { get; private set; }

    /// <summary>Overwrites what research knows about the candidate. Deliberately leaves <see cref="Status"/> alone.</summary>
    public void ApplyResearch(
        string title, string? organization, string? location, string? url, string? applyUrl,
        JobPlatform? platform, string? externalId, string? description,
        int score, int coverage, FilterOutcome outcome, string? outcomeReason,
        string breakdownJson, string factsJson, string gapsJson, int gapsCount,
        Guid researchJobId, DateTime utcNow)
    {
        if (score is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(score));
        if (coverage is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(coverage));
        Title = Guard.Truncate(Guard.Required(title, int.MaxValue, nameof(title)), MaxTitleLength);
        Organization = Guard.Truncate(organization, MaxOrganizationLength);
        Location = Guard.TruncateOptional(location, MaxLocationLength);
        Url = UrlOrNull(url);
        ApplyUrl = UrlOrNull(applyUrl);
        Platform = platform;
        ExternalId = Guard.TruncateOptional(externalId, MaxExternalIdLength);
        Description = Guard.TruncateOptional(description, MaxDescriptionLength);
        Score = score;
        Coverage = coverage;
        Outcome = outcome;
        OutcomeReason = Guard.TruncateOptional(outcomeReason, MaxOutcomeReasonLength);
        BreakdownJson = string.IsNullOrWhiteSpace(breakdownJson) ? "[]" : breakdownJson;
        FactsJson = string.IsNullOrWhiteSpace(factsJson) ? "[]" : factsJson;
        GapsJson = string.IsNullOrWhiteSpace(gapsJson) ? "[]" : gapsJson;
        GapsCount = Math.Max(0, gapsCount);
        LastResearchJobId = researchJobId;
        UpdatedAt = utcNow;
        Version++;
    }

    /// <summary>A user-chosen pipeline move. Returns the activity to record, or null when the status is unchanged.</summary>
    public Activity? ChangeStatus(OpportunityStatus status, DateTime utcNow)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if (status == Status) return null;
        var from = Status;
        Status = status;
        UpdatedAt = utcNow;
        Version++;
        return new Activity(OwnerId, Id, ActivityKinds.StatusChanged, utcNow, $"{from} → {status}");
    }

    /// <summary>The desktop agent confirmed it submitted the application. Idempotent.</summary>
    public Activity? MarkApplied(string detail, DateTime utcNow)
    {
        if (Status == OpportunityStatus.Applied) return null;
        Status = OpportunityStatus.Applied;
        UpdatedAt = utcNow;
        Version++;
        return new Activity(OwnerId, Id, ActivityKinds.Applied, utcNow, detail);
    }

    // A URL longer than the column is not worth keeping truncated: a cut URL points somewhere else.
    private static string? UrlOrNull(string? url) =>
        string.IsNullOrWhiteSpace(url) || url.Trim().Length > MaxUrlLength ? null : url.Trim();
}
