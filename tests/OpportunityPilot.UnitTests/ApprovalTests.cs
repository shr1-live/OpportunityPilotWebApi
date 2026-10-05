using OpportunityPilot.Application.Approvals;
using OpportunityPilot.Application.Common;
using OpportunityPilot.Domain.Campaigns;
using OpportunityPilot.Domain.Common;
using OpportunityPilot.Domain.Opportunities;

namespace OpportunityPilot.UnitTests;

/// <summary>The batch approval queue: the research suggestion rule, the user's decision, and request validation.</summary>
public class ApprovalTests
{
    private static readonly DateTime T0 = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    private static Opportunity Scored(int score, FilterOutcome outcome = FilterOutcome.Qualified, OpportunityMode mode = OpportunityMode.Job)
    {
        var o = new Opportunity(Guid.NewGuid(), Guid.NewGuid(), mode, "job:Greenhouse:1", T0);
        o.ApplyResearch("Dev", "Acme", "Pune", "https://x.example/1", "https://x.example/1", JobPlatform.Greenhouse, "1", "C#",
            score, 80, outcome, null, "[]", "[]", "[]", 0, Guid.NewGuid(), T0);
        return o;
    }

    [Theory]
    [InlineData(80, 80, true)]     // the threshold itself qualifies
    [InlineData(100, 80, true)]
    [InlineData(79, 80, false)]
    [InlineData(1, 1, true)]
    public void A_new_qualified_job_is_suggested_only_at_or_above_the_threshold(int score, int threshold, bool suggested)
    {
        var o = Scored(score);

        var activity = o.SuggestForApproval(threshold, T0.AddMinutes(1));

        Assert.Equal(suggested, activity is not null);
        Assert.Equal(suggested ? OpportunityStatus.Suggested : OpportunityStatus.New, o.Status);
        if (activity is not null)
        {
            Assert.Equal(ActivityKinds.Suggested, activity.Kind);
            Assert.Equal($"Suggested for approval: scored {score} ≥ {threshold}.", activity.Detail);
            Assert.Equal(T0.AddMinutes(1), o.UpdatedAt);
        }
    }

    [Theory]
    [InlineData(FilterOutcome.NeedsVerification)]
    [InlineData(FilterOutcome.Excluded)]
    public void Only_qualified_opportunities_are_suggested(FilterOutcome outcome)
    {
        var o = Scored(100, outcome);
        Assert.Null(o.SuggestForApproval(50, T0));
        Assert.Equal(OpportunityStatus.New, o.Status);
    }

    [Fact]
    public void Customer_opportunities_are_never_suggested()
    {
        var o = Scored(100, mode: OpportunityMode.Customer);
        Assert.Null(o.SuggestForApproval(50, T0));
        Assert.Equal(OpportunityStatus.New, o.Status);
    }

    [Fact]
    public void Auto_suggest_off_suggests_nothing()
    {
        var o = Scored(100);
        Assert.Null(o.SuggestForApproval(null, T0));
        Assert.Equal(OpportunityStatus.New, o.Status);
    }

    [Theory]
    [InlineData(OpportunityStatus.Suggested)]
    [InlineData(OpportunityStatus.Shortlisted)]
    [InlineData(OpportunityStatus.Dismissed)]
    [InlineData(OpportunityStatus.Applied)]
    [InlineData(OpportunityStatus.Contacted)]
    [InlineData(OpportunityStatus.Closed)]
    public void Research_never_touches_a_status_other_than_new(OpportunityStatus status)
    {
        var o = Scored(100);
        o.ChangeStatus(status, T0);
        var version = o.Version;

        Assert.Null(o.SuggestForApproval(1, T0.AddMinutes(1)));

        Assert.Equal(status, o.Status);
        Assert.Equal(version, o.Version);
    }

    [Fact]
    public void Approving_shortlists_and_rejecting_dismisses_each_with_its_activity()
    {
        var yes = Scored(90);
        var no = Scored(90);
        yes.SuggestForApproval(80, T0);
        no.SuggestForApproval(80, T0);

        var approved = yes.DecideSuggestion(approve: true, T0.AddMinutes(5));
        var rejected = no.DecideSuggestion(approve: false, T0.AddMinutes(5));

        Assert.Equal(OpportunityStatus.Shortlisted, yes.Status);
        Assert.Equal((ActivityKinds.Approved, "Approved: Suggested → Shortlisted."), (approved!.Kind, approved.Detail));
        Assert.Equal(OpportunityStatus.Dismissed, no.Status);
        Assert.Equal((ActivityKinds.Rejected, "Rejected: Suggested → Dismissed."), (rejected!.Kind, rejected.Detail));
    }

    [Theory]
    [InlineData(OpportunityStatus.New)]
    [InlineData(OpportunityStatus.Shortlisted)]
    [InlineData(OpportunityStatus.Dismissed)]
    [InlineData(OpportunityStatus.Applied)]
    public void Only_a_suggested_opportunity_can_be_decided(OpportunityStatus status)
    {
        var o = Scored(90);
        o.ChangeStatus(status, T0);

        Assert.Null(o.DecideSuggestion(approve: true, T0));
        Assert.Null(o.DecideSuggestion(approve: false, T0));
        Assert.Equal(status, o.Status);
    }

    [Fact]
    public void The_campaign_threshold_is_1_to_100_and_job_only()
    {
        Campaign Job(int? min) => new(Guid.NewGuid(), Guid.NewGuid(), OpportunityMode.Job, "C", null, "{}", "{}", 25, T0, min);

        Assert.Equal(80, Job(80).AutoSuggestMinScore);
        Assert.Null(Job(null).AutoSuggestMinScore);
        Assert.Throws<ArgumentOutOfRangeException>(() => Job(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Job(101));
        Assert.Throws<ArgumentException>(() =>
            new Campaign(Guid.NewGuid(), Guid.NewGuid(), OpportunityMode.Customer, "C", null, "{}", "{}", 25, T0, 80));
    }

    [Theory]
    [InlineData(null, AppliesVia.You)]
    [InlineData(JobPlatform.LinkedIn, AppliesVia.Agent)]
    [InlineData(JobPlatform.Naukri, AppliesVia.Agent)]
    [InlineData(JobPlatform.Greenhouse, AppliesVia.You)]
    [InlineData(JobPlatform.Lever, AppliesVia.You)]
    [InlineData(JobPlatform.Adzuna, AppliesVia.You)]
    [InlineData(JobPlatform.Other, AppliesVia.You)]
    public void Only_linkedin_and_naukri_are_applied_to_by_the_agent(JobPlatform? platform, AppliesVia expected) =>
        Assert.Equal(expected, ApprovalService.AppliesViaFor(platform));

    [Fact]
    public void Decisions_dedupe_ids_within_a_list()
    {
        var a = Guid.NewGuid();
        var (approve, reject) = ApprovalService.Validate(new DecideApprovalsRequest([a, a], null));

        Assert.Equal([a], approve);
        Assert.Empty(reject);
    }

    [Fact]
    public void Deciding_nothing_is_rejected()
    {
        var ex = Assert.Throws<RequestValidationException>(() => ApprovalService.Validate(new DecideApprovalsRequest([], null)));
        Assert.True(ex.Errors.ContainsKey("approve"));
        Assert.Throws<RequestValidationException>(() => ApprovalService.Validate(null));
    }

    [Fact]
    public void More_than_200_decisions_are_rejected()
    {
        var approve = Enumerable.Range(0, 150).Select(_ => Guid.NewGuid()).ToList();
        var reject = Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToList();

        var ex = Assert.Throws<RequestValidationException>(() => ApprovalService.Validate(new DecideApprovalsRequest(approve, reject)));
        Assert.Contains("200", ex.Errors["approve"][0]);

        ApprovalService.Validate(new DecideApprovalsRequest(approve, reject.Take(50).ToList()));   // exactly 200 is fine
    }

    [Fact]
    public void The_same_id_cannot_be_approved_and_rejected()
    {
        var id = Guid.NewGuid();
        var ex = Assert.Throws<RequestValidationException>(() => ApprovalService.Validate(new DecideApprovalsRequest([id], [Guid.NewGuid(), id])));
        Assert.True(ex.Errors.ContainsKey("reject"));
    }
}
