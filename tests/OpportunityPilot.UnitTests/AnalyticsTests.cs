using OpportunityPilot.Application.Analytics;
using OpportunityPilot.Application.Common;
using OpportunityPilot.Application.Research.Rules;
using OpportunityPilot.Domain.Opportunities;

namespace OpportunityPilot.UnitTests;

public class AnalyticsTests
{
    private static readonly DateTime T0 = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("Candidate", 30, AnalyticsWorkspace.Candidate)]
    [InlineData(" sales ", 365, AnalyticsWorkspace.Sales)]
    public void Workspace_and_window_are_validated(string workspace, int days, AnalyticsWorkspace expected) =>
        Assert.Equal(expected, AnalyticsService.Validate(workspace, days));

    [Theory]
    [InlineData(null, 30)]
    [InlineData("unknown", 30)]
    [InlineData("Candidate", 0)]
    [InlineData("Sales", 366)]
    public void Invalid_workspace_or_window_is_rejected(string? workspace, int days) =>
        Assert.Throws<RequestValidationException>(() => AnalyticsService.Validate(workspace, days));

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(9, 0)]
    [InlineData(10, 1)]
    [InlineData(70, 7)]
    [InlineData(99, 9)]
    [InlineData(100, 9)]
    [InlineData(101, 9)]
    public void Histogram_bands_clamp_and_include_one_hundred(int score, int expected) =>
        Assert.Equal(expected, AnalyticsService.Band(score));

    [Fact]
    public void Histogram_always_returns_ten_bands()
    {
        var bands = AnalyticsService.Histogram([0, 9, 10, 70, 99, 100]);

        Assert.Equal(10, bands.Count);
        Assert.Equal((0, 9, 2), (bands[0].From, bands[0].To, bands[0].Count));
        Assert.Equal((70, 79, 1), (bands[7].From, bands[7].To, bands[7].Count));
        Assert.Equal((90, 100, 2), (bands[9].From, bands[9].To, bands[9].Count));
    }

    [Theory]
    [InlineData(1, 4, 0.25)]
    [InlineData(2, 3, 0.6667)]
    [InlineData(0, 0, null)]
    public void Rates_are_rounded_and_zero_denominators_are_unknown(int numerator, int denominator, double? expected) =>
        Assert.Equal(expected, AnalyticsService.Rate(numerator, denominator));

    [Fact]
    public void Reached_uses_history_when_the_current_status_is_off_pipeline()
    {
        var history = new[]
        {
            new AnalyticsActivity(ActivityKinds.StatusChanged, "New → Shortlisted", T0),
            new AnalyticsActivity(ActivityKinds.StatusChanged, "Shortlisted → Dismissed", T0.AddMinutes(1))
        };

        Assert.True(AnalyticsService.Reached(OpportunityStatus.Dismissed, history, OpportunityStatus.Shortlisted));
        Assert.False(AnalyticsService.Reached(OpportunityStatus.Dismissed, history, OpportunityStatus.Applied));
    }

    [Fact]
    public void First_reach_ignores_malformed_and_non_status_activity()
    {
        var expected = new AnalyticsActivity(ActivityKinds.Applied, "Applied by agent", T0.AddMinutes(2));
        var history = new[]
        {
            new AnalyticsActivity(ActivityKinds.Researched, "Researched", T0),
            new AnalyticsActivity(ActivityKinds.StatusChanged, "malformed", T0.AddMinutes(1)),
            expected
        };

        Assert.Equal(expected, AnalyticsService.FirstReach(history, OpportunityStatus.Applied));
    }

    [Fact]
    public void Unknown_criteria_are_counted_once_per_opportunity_and_sorted()
    {
        var rows = new[]
        {
            (IReadOnlyList<BreakdownRow>)[new("location", "Location", 10, null, 0, "Unknown", []),
                new("skills", "Skills", 40, null, 0, "Unknown", []),
                new("skills", "Skills", 40, null, 0, "Duplicate", [])],
            (IReadOnlyList<BreakdownRow>)[new("skills", "Skills", 40, null, 0, "Unknown", [])]
        };

        var result = AnalyticsService.TopUnknown(rows);

        Assert.Equal([("skills", 2), ("location", 1)], result.Select(x => (x.Criterion, x.UnknownCount)));
    }
}
