using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Research;

/// <summary>
/// One durable research run. A processor claims it with a lease (guarded by <see cref="Version"/>), renews the
/// lease while working, and an expired lease lets another processor reclaim it after a crash or restart.
/// <see cref="Attempts"/> counts claims, so a processor whose lease was taken over can tell it lost the job.
/// </summary>
public class ResearchJob : IOwned
{
    public const int MaxSafeErrorLength = 500;
    public const int MaxAttempts = 3;

    private ResearchJob() { }

    public ResearchJob(Guid ownerId, Guid campaignId, DateTime utcNow)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerId));
        if (campaignId == Guid.Empty) throw new ArgumentException("Campaign is required.", nameof(campaignId));
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        CampaignId = campaignId;
        State = ResearchJobState.Queued;
        Stage = ResearchStage.Prepare;
        CreatedAt = utcNow;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid CampaignId { get; private set; }
    public ResearchJobState State { get; private set; }
    public ResearchStage Stage { get; private set; }
    public string CountsJson { get; private set; } = "{}";
    public int Attempts { get; private set; }
    public DateTime? LeaseUntil { get; private set; }
    public bool CancelRequested { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public string? SafeError { get; private set; }
    public int Version { get; private set; }

    public bool IsActive => State is ResearchJobState.Queued or ResearchJobState.Running;

    public bool IsTerminal => !IsActive;

    /// <summary>Queued, or Running whose lease has run out (the processor that held it stopped).</summary>
    public bool CanBeClaimed(DateTime utcNow) =>
        State == ResearchJobState.Queued || (State == ResearchJobState.Running && (LeaseUntil is null || LeaseUntil < utcNow));

    /// <summary>A reclaimed job that has already been tried <see cref="MaxAttempts"/> times should be failed, not retried.</summary>
    public bool AttemptsExhausted => Attempts >= MaxAttempts;

    public void Claim(DateTime utcNow, TimeSpan lease)
    {
        if (!CanBeClaimed(utcNow)) throw new InvalidOperationException("Job is not claimable.");
        if (lease <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(lease));
        State = ResearchJobState.Running;
        StartedAt ??= utcNow;
        Attempts++;
        LeaseUntil = utcNow + lease;
        Version++;
    }

    /// <summary>Persists progress and extends the lease. Only the processor holding the job calls this.</summary>
    public void Progress(ResearchStage stage, string countsJson, DateTime utcNow, TimeSpan lease)
    {
        if (State != ResearchJobState.Running) throw new InvalidOperationException("Only a running job reports progress.");
        if (stage == ResearchStage.Complete) throw new ArgumentException("Use Finish to complete a job.", nameof(stage));
        Stage = stage;
        CountsJson = string.IsNullOrWhiteSpace(countsJson) ? "{}" : countsJson;
        LeaseUntil = utcNow + lease;
        Version++;
    }

    public void Finish(ResearchJobState state, string? safeError, DateTime utcNow)
    {
        if (State != ResearchJobState.Running) throw new InvalidOperationException("Only a running job can finish.");
        if (state is ResearchJobState.Queued or ResearchJobState.Running)
            throw new ArgumentException("Finish needs a final state.", nameof(state));
        State = state;
        Stage = ResearchStage.Complete;
        SafeError = Guard.TruncateOptional(safeError, MaxSafeErrorLength);
        FinishedAt = utcNow;
        LeaseUntil = null;
        Version++;
    }

    /// <summary>
    /// A queued job is cancelled at once (nothing ran, nothing to keep). A running job is flagged and the processor
    /// stops between items, keeping what it already wrote. Finished jobs are left alone. Returns whether anything changed.
    /// </summary>
    public bool RequestCancel(DateTime utcNow)
    {
        switch (State)
        {
            case ResearchJobState.Queued:
                CancelRequested = true;
                State = ResearchJobState.Cancelled;
                Stage = ResearchStage.Complete;
                FinishedAt = utcNow;
                Version++;
                return true;
            case ResearchJobState.Running when !CancelRequested:
                CancelRequested = true;
                Version++;
                return true;
            default:
                return false;
        }
    }
}
