using OpportunityPilot.Domain.Common;
using OpportunityPilot.Domain.Opportunities;
using OpportunityPilot.Domain.Research;

namespace OpportunityPilot.UnitTests.Research;

public class ResearchDomainTests
{
    private static readonly DateTime T0 = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);

    private static ResearchJob NewJob() => new(Guid.NewGuid(), Guid.NewGuid(), T0);

    [Fact]
    public void A_claim_starts_the_job_takes_a_lease_and_bumps_the_version()
    {
        var job = NewJob();
        var v = job.Version;

        job.Claim(T0, Lease);

        Assert.Equal(ResearchJobState.Running, job.State);
        Assert.Equal(T0 + Lease, job.LeaseUntil);
        Assert.Equal(1, job.Attempts);
        Assert.Equal(T0, job.StartedAt);
        Assert.True(job.Version > v);
    }

    [Fact]
    public void A_running_job_can_be_reclaimed_only_after_its_lease_expires()
    {
        var job = NewJob();
        job.Claim(T0, Lease);

        Assert.False(job.CanBeClaimed(T0.AddMinutes(1)));
        Assert.Throws<InvalidOperationException>(() => job.Claim(T0.AddMinutes(1), Lease));

        Assert.True(job.CanBeClaimed(T0.AddMinutes(3)));
        job.Claim(T0.AddMinutes(3), Lease);
        Assert.Equal(2, job.Attempts);
        Assert.Equal(T0, job.StartedAt);          // first start is kept
    }

    [Fact]
    public void Progress_renews_the_lease()
    {
        var job = NewJob();
        job.Claim(T0, Lease);
        job.Progress(ResearchStage.Gather, "{\"sources\":1}", T0.AddMinutes(1), Lease);
        Assert.Equal(T0.AddMinutes(3), job.LeaseUntil);
        Assert.Equal(ResearchStage.Gather, job.Stage);
    }

    [Fact]
    public void Cancelling_a_queued_job_finishes_it_at_once()
    {
        var job = NewJob();
        Assert.True(job.RequestCancel(T0));
        Assert.Equal(ResearchJobState.Cancelled, job.State);
        Assert.False(job.CanBeClaimed(T0));
        Assert.False(job.RequestCancel(T0));      // idempotent
    }

    [Fact]
    public void Cancelling_a_running_job_only_flags_it_for_the_processor()
    {
        var job = NewJob();
        job.Claim(T0, Lease);
        Assert.True(job.RequestCancel(T0));
        Assert.Equal(ResearchJobState.Running, job.State);
        Assert.True(job.CancelRequested);

        job.Finish(ResearchJobState.Cancelled, null, T0.AddSeconds(5));
        Assert.Equal(ResearchStage.Complete, job.Stage);
        Assert.Null(job.LeaseUntil);
        Assert.False(job.RequestCancel(T0.AddSeconds(6)));
    }

    [Fact]
    public void Only_a_running_job_finishes_and_only_with_a_final_state()
    {
        var job = NewJob();
        Assert.Throws<InvalidOperationException>(() => job.Finish(ResearchJobState.Completed, null, T0));
        job.Claim(T0, Lease);
        Assert.Throws<ArgumentException>(() => job.Finish(ResearchJobState.Queued, null, T0));
    }

    private static Opportunity Researched(int score = 50)
    {
        var o = new Opportunity(Guid.NewGuid(), Guid.NewGuid(), OpportunityMode.Job, "job:LinkedIn:1", T0);
        o.ApplyResearch("Dev", "Acme", "Pune", "https://x.example/1", "https://x.example/1", JobPlatform.LinkedIn, "1", "C#",
            score, 80, FilterOutcome.Qualified, null, "[]", "[]", "[]", 0, Guid.NewGuid(), T0);
        return o;
    }

    [Fact]
    public void Rerunning_research_never_resets_a_status_the_user_chose()
    {
        var o = Researched();
        Assert.NotNull(o.ChangeStatus(OpportunityStatus.Shortlisted, T0.AddMinutes(1)));

        o.ApplyResearch("Dev II", "Acme", "Pune", null, null, JobPlatform.LinkedIn, "1", "C#", 90, 100, FilterOutcome.Qualified,
            null, "[]", "[]", "[]", 0, Guid.NewGuid(), T0.AddMinutes(2));

        Assert.Equal(OpportunityStatus.Shortlisted, o.Status);
        Assert.Equal(90, o.Score);
        Assert.Equal("Dev II", o.Title);
    }

    [Fact]
    public void Status_changes_record_an_activity_and_unchanged_status_records_none()
    {
        var o = Researched();
        var activity = o.ChangeStatus(OpportunityStatus.Dismissed, T0);
        Assert.Equal(ActivityKinds.StatusChanged, activity!.Kind);
        Assert.Equal("New → Dismissed", activity.Detail);
        Assert.Null(o.ChangeStatus(OpportunityStatus.Dismissed, T0));
    }

    [Fact]
    public void Agent_confirmation_marks_applied_once()
    {
        var o = Researched();
        o.ChangeStatus(OpportunityStatus.Shortlisted, T0);
        var applied = o.MarkApplied("Applied on LinkedIn", T0.AddHours(1));
        Assert.Equal(ActivityKinds.Applied, applied!.Kind);
        Assert.Equal(OpportunityStatus.Applied, o.Status);
        Assert.Null(o.MarkApplied("again", T0.AddHours(2)));
    }

    [Fact]
    public void Evidence_hash_is_the_sha256_of_the_stored_excerpt()
    {
        var long_ = new string('x', 5000);
        var e = new Evidence(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, T0, long_, Evidence.RulesMethod);
        Assert.Equal(2000, e.Excerpt.Length);
        Assert.Equal(64, e.ContentHash.Length);
        Assert.Equal(Evidence.HashOf(long_), e.ContentHash);
        Assert.NotEqual(Evidence.HashOf("a"), Evidence.HashOf("b"));
    }

    [Fact]
    public void An_import_commits_once_and_not_after_it_expires()
    {
        var batch = new ImportBatch(Guid.NewGuid(), Guid.NewGuid(), "[]", T0);
        Assert.Throws<InvalidOperationException>(() => new ImportBatch(Guid.NewGuid(), Guid.NewGuid(), "[]", T0).Commit(T0.AddHours(2)));
        batch.Commit(T0.AddMinutes(10));
        Assert.Throws<InvalidOperationException>(() => batch.Commit(T0.AddMinutes(11)));
    }
}
