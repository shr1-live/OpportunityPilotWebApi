using OpportunityPilot.Application.Campaigns;
using OpportunityPilot.Application.Research;
using OpportunityPilot.Domain.Common;
using OpportunityPilot.Domain.Opportunities;

namespace OpportunityPilot.UnitTests.Research;

public class CampaignSettingsTests
{
    [Fact]
    public void Missing_weights_use_the_mode_defaults()
    {
        var errors = new Dictionary<string, string[]>();
        var w = CampaignWeights.Normalise(OpportunityMode.Job, null, errors);
        Assert.Empty(errors);
        Assert.Equal(40, w[CampaignWeights.MandatorySkills]);
        Assert.Equal(100, w.Values.Sum());
        Assert.Equal(100, CampaignWeights.Defaults(OpportunityMode.Customer).Values.Sum());
    }

    [Fact]
    public void Weights_are_scaled_to_exactly_100_with_integer_values()
    {
        var errors = new Dictionary<string, string[]>();
        var w = CampaignWeights.Normalise(OpportunityMode.Job, new Dictionary<string, double>
        {
            ["mandatorySkills"] = 1, ["experience"] = 1, ["location"] = 1, ["preferredSkills"] = 0
        }, errors);

        Assert.Empty(errors);
        Assert.Equal(100, w.Values.Sum());
        Assert.Equal(0, w["preferredSkills"]);
        Assert.All(w.Where(kv => kv.Key != "preferredSkills"), kv => Assert.InRange(kv.Value, 33, 34));
    }

    [Theory]
    [InlineData("industry", 10)]      // not a Job criterion
    [InlineData("experience", 150)]   // out of range
    [InlineData("experience", -1)]
    public void Invalid_weights_are_rejected_with_a_field_error(string key, double value)
    {
        var errors = new Dictionary<string, string[]>();
        CampaignWeights.Normalise(OpportunityMode.Job, new Dictionary<string, double> { [key] = value, ["mandatorySkills"] = 50 }, errors);
        Assert.Contains(errors.Keys, k => k.StartsWith("weights.", StringComparison.Ordinal));
    }

    [Fact]
    public void All_zero_weights_are_rejected()
    {
        var errors = new Dictionary<string, string[]>();
        CampaignWeights.Normalise(OpportunityMode.Customer, new Dictionary<string, double> { ["industry"] = 0 }, errors);
        Assert.True(errors.ContainsKey("weights"));
    }

    [Fact]
    public void Criteria_are_trimmed_deduplicated_and_work_modes_canonicalised()
    {
        var errors = new Dictionary<string, string[]>();
        var c = CampaignCriteria.Normalise(new CampaignCriteria
        {
            RequiredSkills = [" C# ", "c#", "", ".NET"],
            WorkModes = ["remote", "on-site"]
        }, errors);

        Assert.Empty(errors);
        Assert.Equal(["C#", ".NET"], c.RequiredSkills);
        Assert.Equal(["Remote", "Onsite"], c.WorkModes);
    }

    [Fact]
    public void Unknown_work_mode_and_impossible_years_are_field_errors()
    {
        var errors = new Dictionary<string, string[]>();
        CampaignCriteria.Normalise(new CampaignCriteria { WorkModes = ["Moon"], CandidateYears = -2 }, errors);
        Assert.True(errors.ContainsKey("criteria.workModes"));
        Assert.True(errors.ContainsKey("criteria.candidateYears"));
    }

    [Fact]
    public void Criteria_round_trip_through_json()
    {
        var c = new CampaignCriteria { RequiredSkills = ["C#"], CandidateYears = 4.5, Locations = ["Pune"] };
        var back = CampaignCriteria.FromJson(c.ToJson());
        Assert.Equal(["C#"], back.RequiredSkills);
        Assert.Equal(4.5, back.CandidateYears);
        Assert.Contains("\"requiredSkills\"", c.ToJson());
    }

    [Theory]
    [InlineData(OpportunityMode.Job, JobPlatform.LinkedIn, "4012", "Dev", "Acme", null, "job:LinkedIn:4012")]
    [InlineData(OpportunityMode.Job, null, "4012", "Senior  Dev!", "Acme, Inc.", null, "senior dev|acme inc")]
    [InlineData(OpportunityMode.Customer, null, null, "Ledgerly", "Ledgerly", "https://www.Ledgerly.example/about", "ledgerly.example")]
    [InlineData(OpportunityMode.Customer, null, null, "Ledgerly", "Ledgerly", "ledgerly.example", "ledgerly.example")]
    [InlineData(OpportunityMode.Customer, null, null, "Ledgerly Ltd.", "Ledgerly Ltd.", null, "ledgerly ltd")]
    public void Dedupe_keys_follow_the_contract(
        OpportunityMode mode, JobPlatform? platform, string? externalId, string title, string org, string? website, string expected) =>
        Assert.Equal(expected, Candidates.DedupeKey(mode, platform, externalId, title, org, website));

    [Fact]
    public void Research_limits_cannot_exceed_server_ceilings()
    {
        var o = new ResearchOptions { MaxCandidates = 10_000, MaxFetches = 999, TimeoutSeconds = 600, MaxBytes = int.MaxValue, Concurrency = 64 };
        Assert.Equal(100, o.EffectiveCandidates);
        Assert.Equal(50, o.EffectiveFetches);
        Assert.Equal(TimeSpan.FromSeconds(30), o.EffectiveTimeout);
        Assert.Equal(1024 * 1024, o.EffectiveBytes);
        Assert.Equal(4, o.EffectiveConcurrency);

        var lowered = new ResearchOptions { MaxCandidates = 5 };
        Assert.Equal(5, lowered.EffectiveCandidates);
    }
}
