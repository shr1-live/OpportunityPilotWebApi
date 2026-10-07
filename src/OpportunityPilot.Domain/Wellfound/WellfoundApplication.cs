using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Wellfound;

public enum WellfoundApplicationState { New, Reviewing, Shortlisted, Interviewing, Offered, Hired, Rejected }

/// <summary>A recruiter-side application projection; live writes remain disabled until Wellfound OAuth is authorized.</summary>
public sealed class WellfoundApplication : IOwned
{
    public const int MaxProviderIdLength = 200;
    public const int MaxNameLength = 300;
    private WellfoundApplication() { }
    public WellfoundApplication(Guid ownerId, Guid jobId, string providerApplicationId, string candidateName,
        int? fitScore, bool isDemo, DateTime utcNow)
    {
        if (ownerId == Guid.Empty || jobId == Guid.Empty) throw new ArgumentException("Owner and job are required.");
        Id = Guid.NewGuid(); OwnerId = ownerId; JobId = jobId;
        ProviderApplicationId = Guard.Required(providerApplicationId, MaxProviderIdLength, nameof(providerApplicationId));
        CandidateName = Guard.Required(candidateName, MaxNameLength, nameof(candidateName));
        FitScore = fitScore is >= 0 and <= 100 ? fitScore : null;
        State = WellfoundApplicationState.New; EvidenceJson = "{}"; IsDemo = isDemo;
        CreatedAt = utcNow; UpdatedAt = utcNow;
    }
    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid JobId { get; private set; }
    public string ProviderApplicationId { get; private set; } = string.Empty;
    public string CandidateName { get; private set; } = string.Empty;
    public int? FitScore { get; private set; }
    public WellfoundApplicationState State { get; private set; }
    public string EvidenceJson { get; private set; } = "{}";
    public bool IsDemo { get; private set; }
    public int Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public void ChangeState(WellfoundApplicationState state, int expectedVersion, DateTime utcNow)
    {
        if (Version != expectedVersion) throw new InvalidOperationException($"Application was changed elsewhere (now version {Version}). Reload before saving.");
        State = state; Version++; UpdatedAt = utcNow;
    }
}

public enum WellfoundActivityKind { Imported, JobStateChanged, ApplicationStateChanged, SyncObserved }

/// <summary>Immutable local audit; a demo action never claims it changed Wellfound.</summary>
public sealed class WellfoundActivity : IOwned
{
    public const int MaxDetailLength = 1_000;
    private WellfoundActivity() { }
    public WellfoundActivity(Guid ownerId, WellfoundActivityKind kind, string detail, DateTime occurredAt,
        Guid? jobId = null, Guid? applicationId = null, bool providerConfirmed = false)
    {
        Id = Guid.NewGuid(); OwnerId = ownerId; Kind = kind;
        Detail = Guard.Required(detail, MaxDetailLength, nameof(detail)); OccurredAt = occurredAt;
        JobId = jobId; ApplicationId = applicationId; ProviderConfirmed = providerConfirmed;
    }
    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid? JobId { get; private set; }
    public Guid? ApplicationId { get; private set; }
    public WellfoundActivityKind Kind { get; private set; }
    public string Detail { get; private set; } = string.Empty;
    public bool ProviderConfirmed { get; private set; }
    public DateTime OccurredAt { get; private set; }
}
