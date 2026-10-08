using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Automation;

/// <summary>A durable, owner-scoped trigger for one campaign. A short lease prevents two API instances queueing it together.</summary>
public sealed class CampaignSchedule : IOwned
{
    public const int MaxTimeZoneLength = 100;
    public const int MaxSafeErrorLength = 500;
    public const int MinCadenceMinutes = 15;
    public const int MaxCadenceMinutes = 10_080;

    private CampaignSchedule() { }

    public CampaignSchedule(Guid ownerId, Guid campaignId, string timeZone, int cadenceMinutes, DateTime nextRunAt, bool paused, DateTime utcNow)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerId));
        if (campaignId == Guid.Empty) throw new ArgumentException("Campaign is required.", nameof(campaignId));
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        CampaignId = campaignId;
        CreatedAt = utcNow;
        Version = 1;
        Update(timeZone, cadenceMinutes, nextRunAt, paused, utcNow, increment: false);
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid CampaignId { get; private set; }
    public string TimeZone { get; private set; } = "UTC";
    public int CadenceMinutes { get; private set; }
    public DateTime NextRunAt { get; private set; }
    public bool Paused { get; private set; }
    public DateTime? LeaseUntil { get; private set; }
    public DateTime? LastQueuedAt { get; private set; }
    public string? LastSafeError { get; private set; }
    /// <summary>Runs skipped at the last claim because the worker was down past them (they are not run late).</summary>
    public int LastMissedRuns { get; private set; }
    public int TotalMissedRuns { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public int Version { get; private set; }

    public void Update(string timeZone, int cadenceMinutes, DateTime nextRunAt, bool paused, DateTime utcNow) =>
        Update(timeZone, cadenceMinutes, nextRunAt, paused, utcNow, increment: true);

    public bool CanClaim(DateTime utcNow) => !Paused && NextRunAt <= utcNow && (LeaseUntil is null || LeaseUntil <= utcNow);

    public void Claim(DateTime utcNow, TimeSpan lease)
    {
        if (!CanClaim(utcNow)) throw new InvalidOperationException("Schedule is not due.");
        if (lease <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(lease));
        LeaseUntil = utcNow + lease;
        UpdatedAt = utcNow;
        Version++;
    }

    public void Complete(DateTime utcNow, string? safeError)
    {
        LastQueuedAt = safeError is null ? utcNow : LastQueuedAt;
        LastSafeError = Guard.TruncateOptional(safeError, MaxSafeErrorLength);
        LeaseUntil = null;
        // One run is queued now; any other slot that passed while nobody was running is counted, not run late.
        var next = NextRunAt;
        var skipped = -1;
        do { next = next.AddMinutes(CadenceMinutes); skipped++; } while (next <= utcNow);
        NextRunAt = next;
        LastMissedRuns = skipped;
        TotalMissedRuns += skipped;
        UpdatedAt = utcNow;
        Version++;
    }

    private void Update(string timeZone, int cadenceMinutes, DateTime nextRunAt, bool paused, DateTime utcNow, bool increment)
    {
        TimeZone = Guard.Required(timeZone, MaxTimeZoneLength, nameof(timeZone));
        if (cadenceMinutes is < MinCadenceMinutes or > MaxCadenceMinutes) throw new ArgumentOutOfRangeException(nameof(cadenceMinutes));
        CadenceMinutes = cadenceMinutes;
        NextRunAt = nextRunAt;
        Paused = paused;
        LeaseUntil = null;
        LastSafeError = null;
        UpdatedAt = utcNow;
        if (increment) Version++;
    }
}
