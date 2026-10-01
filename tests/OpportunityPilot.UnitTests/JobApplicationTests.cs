using OpportunityPilot.Domain.Applications;

namespace OpportunityPilot.UnitTests;

public class JobApplicationTests
{
    private static readonly DateTime T0 = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    private static JobApplication New(ApplicationStatus status) =>
        new(Guid.NewGuid(), ApplicationPlatform.LinkedIn, " 4012345678 ", "https://www.linkedin.com/jobs/view/4012345678/",
            " Backend Engineer ", "Acme", null, status, "Submitted", T0, T0);

    [Fact]
    public void New_application_trims_text_fields()
    {
        var a = New(ApplicationStatus.Applied);

        Assert.Equal("4012345678", a.ExternalJobId);
        Assert.Equal("Backend Engineer", a.Title);
        Assert.Equal(T0, a.UpdatedAt);
    }

    [Theory]
    [InlineData(ApplicationStatus.DryRun)]
    [InlineData(ApplicationStatus.NeedsManual)]
    [InlineData(ApplicationStatus.Skipped)]
    [InlineData(ApplicationStatus.Failed)]
    public void Applied_is_never_downgraded_by_a_later_report(ApplicationStatus later)
    {
        var a = New(ApplicationStatus.Applied);

        a.ApplyReport("https://www.linkedin.com/jobs/view/4012345678/", "Senior Backend Engineer", "Acme", "Pune",
            later, "Would apply", T0.AddHours(1), T0.AddHours(2));

        Assert.Equal(ApplicationStatus.Applied, a.Status);
        Assert.Equal(T0, a.OccurredAt);             // when it was actually submitted
        Assert.Equal("Submitted", a.Detail);
        Assert.Equal("Senior Backend Engineer", a.Title); // job details still refresh
        Assert.Equal(T0.AddHours(2), a.UpdatedAt);
    }

    [Fact]
    public void Non_applied_status_follows_the_latest_report_and_can_become_applied()
    {
        var a = New(ApplicationStatus.DryRun);

        a.ApplyReport(a.JobUrl, a.Title, a.Company, null, ApplicationStatus.NeedsManual, "Unknown question", T0.AddHours(1), T0.AddHours(1));
        Assert.Equal(ApplicationStatus.NeedsManual, a.Status);

        a.ApplyReport(a.JobUrl, a.Title, a.Company, null, ApplicationStatus.Applied, null, T0.AddHours(2), T0.AddHours(2));
        Assert.Equal(ApplicationStatus.Applied, a.Status);
        Assert.Equal(T0.AddHours(2), a.OccurredAt);
        Assert.Null(a.Detail);
    }
}
