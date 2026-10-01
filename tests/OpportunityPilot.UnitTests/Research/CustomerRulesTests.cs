using OpportunityPilot.Application.Campaigns;
using OpportunityPilot.Application.Research.Rules;
using OpportunityPilot.Domain.Common;
using OpportunityPilot.Domain.Opportunities;

namespace OpportunityPilot.UnitTests.Research;

public class CustomerRulesTests
{
    private const string Ev = "ev-7";
    private static readonly IReadOnlyDictionary<string, int> Defaults = CampaignWeights.Defaults(OpportunityMode.Customer);

    private static readonly CampaignCriteria Criteria = new()
    {
        Industries = ["fintech"], Problems = ["reconciliation", "invoicing"], Locations = ["India"], Signals = ["hiring", "funding"]
    };

    private static RuleInput Company(string text, string? country = "India", string? website = "https://ledgerly.example",
        string? industry = "Fintech", string name = "Ledgerly", IReadOnlyList<string>? links = null) =>
        new(name, name, null, text, Ev, website, country, industry, links);

    private static RuleResult Run(RuleInput input, CampaignCriteria? c = null) => CustomerRules.Evaluate(c ?? Criteria, Defaults, input);

    private static double? Value(RuleResult r, string criterion) => r.Breakdown.Single(b => b.Criterion == criterion).Value;

    [Fact]
    public void Plan_example_scores_65_with_80_percent_coverage()
    {
        // industry 25 + problem 0.5 × 30 + geography 15 + signal unknown 0 + contact 10 = 65 (plan §12).
        var r = Run(Company("Ledgerly automates invoicing for small businesses."));

        Assert.Equal(1, Value(r, CampaignWeights.Industry));
        Assert.Equal(0.5, Value(r, CampaignWeights.Problem));
        Assert.Equal(1, Value(r, CampaignWeights.Geography));
        Assert.Null(Value(r, CampaignWeights.Signal));
        Assert.Equal(1, Value(r, CampaignWeights.ContactPath));
        Assert.Equal(65, r.Score);
        Assert.Equal(80, r.Coverage);
        Assert.Equal(FilterOutcome.Qualified, r.Outcome);
        Assert.Contains("No published signal found", r.Gaps);
    }

    [Fact]
    public void Two_problem_keywords_or_the_only_one_configured_is_full_credit()
    {
        Assert.Equal(1, Value(Run(Company("Invoicing and reconciliation in one tool.")), CampaignWeights.Problem));
        Assert.Equal(0, Value(Run(Company("A payments app.")), CampaignWeights.Problem));

        var single = Criteria with { Problems = ["invoicing"] };
        Assert.Equal(1, Value(Run(Company("Invoicing for SMEs."), single), CampaignWeights.Problem));
    }

    [Fact]
    public void Industry_is_zero_when_text_exists_without_a_match_and_unknown_when_there_is_no_text()
    {
        Assert.Equal(0, Value(Run(Company("We sell shoes.", industry: "Retail")), CampaignWeights.Industry));

        var empty = Run(Company("", industry: null));
        Assert.Null(Value(empty, CampaignWeights.Industry));
        Assert.Null(Value(empty, CampaignWeights.Problem));
    }

    [Fact]
    public void Geography_known_other_fails_and_unknown_needs_verification()
    {
        var other = Run(Company("Invoicing.", country: "Germany"));
        Assert.Equal(0, Value(other, CampaignWeights.Geography));
        Assert.Equal(FilterOutcome.Excluded, other.Outcome);

        var unknown = Run(Company("Invoicing.", country: null));
        Assert.Null(Value(unknown, CampaignWeights.Geography));
        Assert.Equal(FilterOutcome.NeedsVerification, unknown.Outcome);
    }

    [Fact]
    public void A_published_signal_counts_but_its_absence_is_unknown()
    {
        Assert.Equal(1, Value(Run(Company("We are hiring engineers to scale invoicing.")), CampaignWeights.Signal));
        Assert.Null(Value(Run(Company("Invoicing.")), CampaignWeights.Signal));
    }

    [Theory]
    [InlineData(null, "Write to sales@ledgerly.example", true)]
    [InlineData(null, "Please contact us for a demo", true)]
    [InlineData(null, "See https://ledgerly.example/careers for roles", true)]
    [InlineData("https://ledgerly.example", "", true)]
    [InlineData(null, "Nothing here", false)]
    public void Contact_path_needs_a_website_link_email_or_contact_mention(string? website, string text, bool known)
    {
        var r = Run(Company(text, website: website));
        Assert.Equal(known ? 1 : null, Value(r, CampaignWeights.ContactPath));
    }

    [Fact]
    public void A_contact_link_found_on_a_page_counts()
    {
        var r = Run(Company("Invoicing.", website: null, links: ["https://ledgerly.example/contact"]));
        Assert.Equal(1, Value(r, CampaignWeights.ContactPath));
    }

    [Fact]
    public void Contact_path_is_always_applicable_even_with_nothing_else_configured()
    {
        var r = Run(Company("Anything"), CampaignCriteria.Empty);
        var row = Assert.Single(r.Breakdown);
        Assert.Equal(CampaignWeights.ContactPath, row.Criterion);
        Assert.Equal(100, r.Score);
    }

    [Fact]
    public void Excluded_organization_is_excluded()
    {
        var r = Run(Company("Invoicing."), Criteria with { ExcludeOrganizations = ["ledger"] });
        Assert.Equal(FilterOutcome.Excluded, r.Outcome);
    }
}
