using OpportunityPilot.Domain.Automation;

namespace OpportunityPilot.UnitTests;

public class CampaignScheduleTests
{
    private static readonly DateTime T0 = new(2026, 10, 8, 9, 0, 0, DateTimeKind.Utc);

    private static CampaignSchedule Daily(bool paused = false) => new(Guid.NewGuid(), Guid.NewGuid(), "Asia/Kolkata", 1440, T0, paused, T0.AddDays(-1));

    [Fact]
    public void A_due_schedule_is_claimed_once_until_its_lease_ends()
    {
        var s = Daily();
        Assert.True(s.CanClaim(T0));
        s.Claim(T0, TimeSpan.FromMinutes(5));
        Assert.False(s.CanClaim(T0.AddMinutes(1)));
        Assert.True(s.CanClaim(T0.AddMinutes(6)));
    }

    [Fact]
    public void Paused_or_not_yet_due_schedules_are_not_claimed()
    {
        Assert.False(Daily(paused: true).CanClaim(T0));
        Assert.False(Daily().CanClaim(T0.AddMinutes(-1)));
    }

    [Fact]
    public void Completing_on_time_moves_one_cadence_and_misses_nothing()
    {
        var s = Daily();
        s.Claim(T0, TimeSpan.FromMinutes(5));
        s.Complete(T0.AddMinutes(1), null);
        Assert.Equal(T0.AddDays(1), s.NextRunAt);
        Assert.Equal(0, s.LastMissedRuns);
        Assert.Equal(T0.AddMinutes(1), s.LastQueuedAt);
    }

    [Fact]
    public void A_worker_that_was_down_runs_once_and_counts_the_missed_slots()
    {
        var s = Daily();
        var late = T0.AddDays(3).AddHours(2);
        s.Claim(late, TimeSpan.FromMinutes(5));
        s.Complete(late, null);
        Assert.Equal(T0.AddDays(4), s.NextRunAt);
        Assert.Equal(3, s.LastMissedRuns);
        Assert.Equal(3, s.TotalMissedRuns);
    }

    [Fact]
    public void A_failed_queue_keeps_the_last_success_and_records_a_safe_reason()
    {
        var s = Daily();
        s.Claim(T0, TimeSpan.FromMinutes(5));
        s.Complete(T0, "Add at least one source before the next scheduled run.");
        Assert.Null(s.LastQueuedAt);
        Assert.Contains("source", s.LastSafeError);
        Assert.Null(s.LeaseUntil);
    }
}
