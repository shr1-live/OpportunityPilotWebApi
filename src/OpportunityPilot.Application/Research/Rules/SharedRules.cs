using OpportunityPilot.Application.Campaigns;

namespace OpportunityPilot.Application.Research.Rules;

/// <summary>Hard filters that apply to every mode.</summary>
internal static class SharedRules
{
    public static void ExcludeKeywords(CampaignCriteria criteria, List<FilterCheck> checks, params string?[] texts)
    {
        if (criteria.ExcludeKeywords.Count == 0) return;
        var hit = TextMatch.Found(criteria.ExcludeKeywords, texts).FirstOrDefault();
        checks.Add(hit is null
            ? new FilterCheck("excludeKeywords", FilterResult.Pass, "No excluded keyword appears.")
            : new FilterCheck("excludeKeywords", FilterResult.Fail, $"Mentions excluded keyword \"{hit}\"."));
    }

    public static void ExcludeOrganizations(CampaignCriteria criteria, List<FilterCheck> checks, string organization)
    {
        if (criteria.ExcludeOrganizations.Count == 0) return;
        if (string.IsNullOrWhiteSpace(organization))
        {
            checks.Add(new FilterCheck("excludeOrganizations", FilterResult.Unknown, "Organization is not stated, so excluded organizations cannot be checked."));
            return;
        }
        var hit = criteria.ExcludeOrganizations.FirstOrDefault(o => organization.Contains(o, StringComparison.OrdinalIgnoreCase));
        checks.Add(hit is null
            ? new FilterCheck("excludeOrganizations", FilterResult.Pass, "Not an excluded organization.")
            : new FilterCheck("excludeOrganizations", FilterResult.Fail, $"Organization matches excluded \"{hit}\"."));
    }
}
