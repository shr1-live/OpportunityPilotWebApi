using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Wellfound;

public enum WellfoundJobScope { CandidateDiscovery, RecruiterOwned }
public enum WellfoundJobState { New, Saved, Applied, Interviewing, Offered, Rejected }

/// <summary>A normalized Wellfound job; demo records are explicit and can never be mistaken for provider-synced data.</summary>
public sealed class WellfoundJob : IOwned
{
    public const int MaxProviderIdLength = 200;
    public const int MaxNameLength = 300;
    public const int MaxShortLength = 200;
    public const int MaxUrlLength = 1_000;
    public const int MaxSummaryLength = 8_000;

    private WellfoundJob() { }

    public WellfoundJob(Guid ownerId, string providerJobId, WellfoundJobScope scope, string title, string companyName,
        string applyUrl, bool isDemo, DateTime utcNow)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerId));
        Id = Guid.NewGuid(); OwnerId = ownerId;
        ProviderJobId = Guard.Required(providerJobId, MaxProviderIdLength, nameof(providerJobId));
        Scope = scope;
        Title = Guard.Required(title, MaxNameLength, nameof(title));
        CompanyName = Guard.Required(companyName, MaxNameLength, nameof(companyName));
        ApplyUrl = Guard.Required(applyUrl, MaxUrlLength, nameof(applyUrl));
        IsDemo = isDemo; State = WellfoundJobState.New; SkillsJson = "[]"; EvidenceJson = "{}";
        CreatedAt = utcNow; UpdatedAt = utcNow;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string ProviderJobId { get; private set; } = string.Empty;
    public WellfoundJobScope Scope { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string CompanyName { get; private set; } = string.Empty;
    public string? CompanyLogoUrl { get; private set; }
    public string? Location { get; private set; }
    public string? RemoteType { get; private set; }
    public decimal? SalaryMin { get; private set; }
    public decimal? SalaryMax { get; private set; }
    public string? Currency { get; private set; }
    public decimal? EquityMin { get; private set; }
    public decimal? EquityMax { get; private set; }
    public string? ExperienceLevel { get; private set; }
    public string? EmploymentType { get; private set; }
    public string? Industry { get; private set; }
    public string? FundingStage { get; private set; }
    public string? EmployeeCount { get; private set; }
    public bool? VisaSponsorship { get; private set; }
    public DateTime? PostedAt { get; private set; }
    public string ApplyUrl { get; private set; } = string.Empty;
    public string? Summary { get; private set; }
    public string SkillsJson { get; private set; } = "[]";
    public string EvidenceJson { get; private set; } = "{}";
    public int? MatchScore { get; private set; }
    public WellfoundJobState State { get; private set; }
    public bool IsDemo { get; private set; }
    public int Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void SetDetails(string? location, string? remoteType, decimal? salaryMin, decimal? salaryMax, string? currency,
        decimal? equityMin, decimal? equityMax, string? experienceLevel, string? employmentType, string? industry,
        string? fundingStage, string? employeeCount, bool? visaSponsorship, DateTime? postedAt, string? summary,
        string skillsJson, string evidenceJson, int? matchScore, DateTime utcNow)
    {
        Location = Guard.Optional(location, MaxShortLength, nameof(location));
        RemoteType = Guard.Optional(remoteType, MaxShortLength, nameof(remoteType));
        SalaryMin = salaryMin; SalaryMax = salaryMax;
        Currency = Guard.Optional(currency?.ToUpperInvariant(), 3, nameof(currency));
        EquityMin = equityMin; EquityMax = equityMax;
        ExperienceLevel = Guard.Optional(experienceLevel, MaxShortLength, nameof(experienceLevel));
        EmploymentType = Guard.Optional(employmentType, MaxShortLength, nameof(employmentType));
        Industry = Guard.Optional(industry, MaxShortLength, nameof(industry));
        FundingStage = Guard.Optional(fundingStage, MaxShortLength, nameof(fundingStage));
        EmployeeCount = Guard.Optional(employeeCount, MaxShortLength, nameof(employeeCount));
        VisaSponsorship = visaSponsorship; PostedAt = postedAt;
        Summary = Guard.Optional(summary, MaxSummaryLength, nameof(summary));
        SkillsJson = string.IsNullOrWhiteSpace(skillsJson) ? "[]" : skillsJson;
        EvidenceJson = string.IsNullOrWhiteSpace(evidenceJson) ? "{}" : evidenceJson;
        MatchScore = matchScore is >= 0 and <= 100 ? matchScore : null;
        Version++; UpdatedAt = utcNow;
    }

    public void ChangeState(WellfoundJobState state, int expectedVersion, DateTime utcNow)
    {
        if (Version != expectedVersion) throw new InvalidOperationException($"Job was changed elsewhere (now version {Version}). Reload before saving.");
        State = state; Version++; UpdatedAt = utcNow;
    }
}
