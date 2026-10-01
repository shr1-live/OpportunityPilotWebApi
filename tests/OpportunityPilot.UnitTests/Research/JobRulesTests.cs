using OpportunityPilot.Application.Campaigns;
using OpportunityPilot.Application.Research.Rules;
using OpportunityPilot.Domain.Common;
using OpportunityPilot.Domain.Opportunities;

namespace OpportunityPilot.UnitTests.Research;

public class JobRulesTests
{
    private const string Ev = "ev-1";
    private static readonly IReadOnlyDictionary<string, int> Defaults = CampaignWeights.Defaults(OpportunityMode.Job);

    private static RuleInput Posting(string text, string? location = "Pune", string title = "Backend Engineer", string org = "Acme") =>
        new(title, org, location, text, Ev);

    private static RuleResult Run(CampaignCriteria c, RuleInput input) => JobRules.Evaluate(c, Defaults, input);

    private static BreakdownRow Row(RuleResult r, string criterion) => r.Breakdown.Single(b => b.Criterion == criterion);

    [Fact]
    public void A_posting_that_meets_everything_scores_100_with_full_coverage_and_qualifies()
    {
        var c = new CampaignCriteria { RequiredSkills = ["C#", ".NET"], PreferredSkills = ["Docker"], CandidateYears = 4, Locations = ["Pune"] };

        var r = Run(c, Posting("We build services in C# on ASP.NET Core. 3-5 years of experience. Docker is a plus."));

        Assert.Equal(FilterOutcome.Qualified, r.Outcome);
        Assert.Equal(100, r.Score);
        Assert.Equal(100, r.Coverage);
        Assert.Equal(4, r.Breakdown.Count);
        Assert.All(r.Breakdown, b => Assert.Equal(1, b.Value));
        Assert.All(r.Breakdown, b => Assert.Equal([Ev], b.EvidenceIds));
        Assert.Equal(100, r.Breakdown.Sum(b => b.Points), 2);
        Assert.Contains(r.Facts, f => f.Key == "experience" && f.Value == "3–5 years" && f.EvidenceId == Ev);
    }

    [Theory]
    [InlineData("C# and Go and Rust", 1.0)]          // all
    [InlineData("C# and Go only", 0.5)]              // 2 of 3 (>= 50%)
    [InlineData("Mostly C#", 0.0)]                   // 1 of 3 (< 50%)
    public void Mandatory_skills_score_by_fraction_found(string text, double expected)
    {
        var c = new CampaignCriteria { RequiredSkills = ["C#", "Go", "Rust"] };
        var row = Row(Run(c, Posting(text)), CampaignWeights.MandatorySkills);
        Assert.Equal(expected, row.Value);
    }

    [Fact]
    public void Exactly_half_the_skills_is_half_credit()
    {
        var c = new CampaignCriteria { RequiredSkills = ["C#", "Go"] };
        Assert.Equal(0.5, Row(Run(c, Posting("C# shop")), CampaignWeights.MandatorySkills).Value);
    }

    [Fact]
    public void Empty_posting_text_makes_skills_unknown_and_the_skill_filter_unknown()
    {
        var c = new CampaignCriteria { RequiredSkills = ["C#"], PreferredSkills = ["Docker"] };

        var r = Run(c, Posting(""));

        Assert.Null(Row(r, CampaignWeights.MandatorySkills).Value);
        Assert.Null(Row(r, CampaignWeights.PreferredSkills).Value);
        Assert.Empty(Row(r, CampaignWeights.MandatorySkills).EvidenceIds);
        Assert.Equal(FilterOutcome.NeedsVerification, r.Outcome);
        Assert.Equal(0, r.Score);
        Assert.Equal(0, r.Coverage);
    }

    [Theory]
    [InlineData("c#", "Strong CSharp background")]
    [InlineData("C#", "c sharp developers")]
    [InlineData(".NET", "dotnet 8 services")]
    [InlineData(".NET", "ASP.NET Core APIs")]
    [InlineData("JavaScript", "modern JS and React")]
    [InlineData("TypeScript", "TS everywhere")]
    [InlineData("C#", "skills: C#, SQL")]
    public void Skill_aliases_and_symbol_boundaries_match(string skill, string text) =>
        Assert.True(TextMatch.Contains(text, skill));

    [Theory]
    [InlineData("Go", "Google Cloud")]
    [InlineData("Java", "JavaScript frontend")]
    [InlineData("C#", "C++ and C")]
    [InlineData("SQL", "PostgreSQLish")]
    public void Skills_need_word_boundaries(string skill, string text) =>
        Assert.False(TextMatch.Contains(text, skill));

    [Theory]
    [InlineData("3-5 years of experience", 3.0, 5.0)]
    [InlineData("3 - 5 yrs", 3.0, 5.0)]
    [InlineData("3 to 5 yrs experience", 3.0, 5.0)]
    [InlineData("2–4 years", 2.0, 4.0)]
    [InlineData("5+ years in backend", 5.0, null)]
    [InlineData("minimum 4 years", 4.0, null)]
    [InlineData("Minimum of 4 years with SQL", 4.0, null)]
    [InlineData("at least 6 yrs", 6.0, null)]
    [InlineData("4 years of relevant experience", 4.0, null)]
    public void Experience_ranges_are_parsed(string text, double min, double? max)
    {
        var range = JobRules.ParseExperience(text);
        Assert.NotNull(range);
        Assert.Equal(min, range.Min);
        Assert.Equal(max, range.Max);
    }

    [Theory]
    [InlineData("Founded 2015-2020 years ago")]
    [InlineData("Great team, free lunch")]
    [InlineData("")]
    public void No_experience_range_means_null(string text) => Assert.Null(JobRules.ParseExperience(text));

    [Theory]
    [InlineData(4, "3-5 years", 1.0)]      // inside
    [InlineData(5, "3-5 years", 1.0)]      // edge
    [InlineData(6, "3-5 years", 0.5)]      // within a year above
    [InlineData(2, "3-5 years", 0.5)]      // within a year below
    [InlineData(7, "3-5 years", 0.0)]      // two years over
    [InlineData(4, "5+ years", 0.5)]
    [InlineData(10, "5+ years", 1.0)]
    [InlineData(2, "minimum 4 years", 0.0)]
    public void Experience_scores_inside_near_or_outside_the_range(double years, string text, double expected)
    {
        var c = new CampaignCriteria { CandidateYears = years };
        Assert.Equal(expected, Row(Run(c, Posting(text)), CampaignWeights.Experience).Value);
    }

    [Fact]
    public void Experience_without_a_range_is_unknown_not_zero_and_lowers_coverage()
    {
        var c = new CampaignCriteria { RequiredSkills = ["C#"], CandidateYears = 4 };

        var r = Run(c, Posting("C# role, great team."));

        Assert.Null(Row(r, CampaignWeights.Experience).Value);
        // mandatory 40 and experience 20 → redistributed to 66.67 and 33.33; only the first is known.
        Assert.Equal(66.67, Row(r, CampaignWeights.MandatorySkills).Weight);
        Assert.Equal(67, r.Score);
        Assert.Equal(67, r.Coverage);
        Assert.Contains("Experience requirement not stated", r.Gaps);
    }

    [Fact]
    public void Criteria_the_user_did_not_configure_are_not_applicable_and_their_weight_is_redistributed()
    {
        var c = new CampaignCriteria { RequiredSkills = ["C#"] };

        var r = Run(c, Posting("C# backend"));

        var row = Assert.Single(r.Breakdown);
        Assert.Equal(CampaignWeights.MandatorySkills, row.Criterion);
        Assert.Equal(100, row.Weight);
        Assert.Equal(100, r.Score);
        Assert.Equal(100, r.Coverage);
    }

    [Fact]
    public void Nothing_configured_scores_zero_without_inventing_a_match()
    {
        var r = Run(CampaignCriteria.Empty, Posting("Anything"));

        Assert.Empty(r.Breakdown);
        Assert.Equal(0, r.Score);
        Assert.Equal(0, r.Coverage);
        Assert.Equal(FilterOutcome.Qualified, r.Outcome);
    }

    [Fact]
    public void Custom_weights_change_the_contributions()
    {
        var weights = new Dictionary<string, int> { [CampaignWeights.MandatorySkills] = 80, [CampaignWeights.Location] = 20, [CampaignWeights.Experience] = 0, [CampaignWeights.PreferredSkills] = 0 };
        var c = new CampaignCriteria { RequiredSkills = ["C#"], Locations = ["Pune"] };

        var r = JobRules.Evaluate(c, weights, Posting("Python only", location: "Pune"));

        Assert.Equal(20, r.Score);   // skills 0 × 80 + location 1 × 20
        Assert.Equal(100, r.Coverage);
    }

    [Fact]
    public void A_known_location_outside_the_list_fails_and_scores_zero()
    {
        var c = new CampaignCriteria { Locations = ["Pune", "Bengaluru"] };

        var r = Run(c, Posting("C# role", location: "Mumbai"));

        Assert.Equal(FilterOutcome.Excluded, r.Outcome);
        Assert.Contains("Mumbai", r.OutcomeReason);
        Assert.Equal(0, Row(r, CampaignWeights.Location).Value);
        Assert.Equal(100, r.Coverage);
    }

    [Fact]
    public void A_missing_location_is_unknown_and_needs_verification()
    {
        var c = new CampaignCriteria { Locations = ["Pune"] };

        var r = Run(c, Posting("C# role", location: null));

        Assert.Equal(FilterOutcome.NeedsVerification, r.Outcome);
        Assert.Null(Row(r, CampaignWeights.Location).Value);
        Assert.Contains("Location not stated", r.Gaps);
    }

    [Fact]
    public void A_location_named_in_the_text_counts_even_without_a_location_field()
    {
        var c = new CampaignCriteria { Locations = ["Pune"] };
        var r = Run(c, Posting("Our office is in Pune.", location: null));
        Assert.Equal(1, Row(r, CampaignWeights.Location).Value);
        Assert.Equal(FilterOutcome.Qualified, r.Outcome);
    }

    [Fact]
    public void Remote_in_the_list_matches_a_remote_posting()
    {
        var c = new CampaignCriteria { Locations = ["Remote"] };
        var r = Run(c, Posting("This is a work from home role.", location: null));
        Assert.Equal("Remote", JobRules.DetectWorkMode("This is a work from home role."));
        Assert.Equal(1, Row(r, CampaignWeights.Location).Value);
        Assert.Equal(FilterOutcome.Qualified, r.Outcome);
    }

    [Theory]
    [InlineData("Hybrid, 3 days in office", "Hybrid")]
    [InlineData("Fully remote team", "Remote")]
    [InlineData("This role is on-site in Pune", "Onsite")]
    [InlineData("Work from office", "Onsite")]
    [InlineData("Nice team", null)]
    public void Work_mode_is_detected_from_keywords(string text, string? expected) =>
        Assert.Equal(expected, JobRules.DetectWorkMode(text));

    [Fact]
    public void A_detected_work_mode_outside_the_list_fails_and_zeroes_location()
    {
        var c = new CampaignCriteria { WorkModes = ["Remote"], Locations = ["Pune"] };

        var r = Run(c, Posting("Hybrid role in Pune", location: "Pune"));

        Assert.Equal(FilterOutcome.Excluded, r.Outcome);
        Assert.Equal(0, Row(r, CampaignWeights.Location).Value);
    }

    [Fact]
    public void An_undetected_work_mode_is_unknown_and_adds_nothing()
    {
        var c = new CampaignCriteria { WorkModes = ["Remote"], Locations = ["Pune"] };

        var r = Run(c, Posting("C# role", location: "Pune"));

        Assert.Equal(FilterOutcome.NeedsVerification, r.Outcome);
        Assert.Equal(1, Row(r, CampaignWeights.Location).Value);   // location alone decides the value
    }

    [Fact]
    public void Required_skills_filter_fails_only_when_none_appear()
    {
        var c = new CampaignCriteria { RequiredSkills = ["C#", "Kubernetes"] };

        Assert.Equal(FilterOutcome.Excluded, Run(c, Posting("Java and Spring")).Outcome);
        var partial = Run(c, Posting("C# services"));
        Assert.Equal(FilterOutcome.Qualified, partial.Outcome);
        Assert.Equal(0.5, Row(partial, CampaignWeights.MandatorySkills).Value);
    }

    [Fact]
    public void Excluded_keywords_and_organizations_exclude_with_a_reason()
    {
        var keywords = Run(new CampaignCriteria { ExcludeKeywords = ["intern"] }, Posting("Paid role", title: "Java Intern"));
        Assert.Equal(FilterOutcome.Excluded, keywords.Outcome);
        Assert.Contains("intern", keywords.OutcomeReason);

        var org = Run(new CampaignCriteria { ExcludeOrganizations = ["Acme"] }, Posting("Role", org: "Acme Corp"));
        Assert.Equal(FilterOutcome.Excluded, org.Outcome);

        var unknownOrg = Run(new CampaignCriteria { ExcludeOrganizations = ["Acme"] }, Posting("Role", org: ""));
        Assert.Equal(FilterOutcome.NeedsVerification, unknownOrg.Outcome);
    }

    [Theory]
    [InlineData("Salary ₹12-18 LPA", "₹12-18 LPA")]
    [InlineData("Pay: $120,000 - $150,000 per year", "$120,000 - $150,000 per year")]
    [InlineData("CTC 10 - 14 LPA", "10 - 14 LPA")]
    public void Salary_is_an_optional_fact(string text, string expected) =>
        Assert.Equal(expected, JobRules.FindSalary(text));

    [Fact]
    public void Fail_wins_over_unknown()
    {
        var c = new CampaignCriteria { Locations = ["Pune"], ExcludeKeywords = ["unpaid"] };
        var r = Run(c, Posting("Unpaid role", location: null));
        Assert.Equal(FilterOutcome.Excluded, r.Outcome);
    }
}
