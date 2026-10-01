using System.Globalization;
using System.Text.RegularExpressions;
using OpportunityPilot.Application.Campaigns;

namespace OpportunityPilot.Application.Research.Rules;

/// <param name="Max">Null for open-ended ranges such as "5+ years" or "minimum 4 years".</param>
public sealed record ExperienceRange(double Min, double? Max)
{
    public override string ToString() => Max is { } max
        ? $"{Min.ToString(CultureInfo.InvariantCulture)}–{max.ToString(CultureInfo.InvariantCulture)} years"
        : $"{Min.ToString(CultureInfo.InvariantCulture)}+ years";
}

/// <summary>Deterministic extraction and scoring for job postings. Pure: same input, same result.</summary>
public static partial class JobRules
{
    public const string Remote = "Remote";
    public const string Hybrid = "Hybrid";
    public const string Onsite = "Onsite";

    public static RuleResult Evaluate(CampaignCriteria criteria, IReadOnlyDictionary<string, int> weights, RuleInput input)
    {
        var ev = input.EvidenceId;
        var hasText = !string.IsNullOrWhiteSpace(input.Text);
        var corpus = $"{input.Title}\n{input.Text}";
        var workMode = DetectWorkMode(corpus, input.Location);
        var experience = hasText ? ParseExperience(corpus) : null;
        var salary = hasText ? FindSalary(input.Text) : null;

        var scores = new List<CriterionScore>();
        var facts = new List<FactRow>();
        var gaps = new List<string>();
        var checks = new List<FilterCheck>();

        // Hard filters.
        SharedRules.ExcludeKeywords(criteria, checks, input.Title, input.Text);
        SharedRules.ExcludeOrganizations(criteria, checks, input.Organization);

        List<string> requiredFound = [];
        if (criteria.RequiredSkills.Count > 0)
        {
            if (!hasText)
            {
                checks.Add(new FilterCheck("requiredSkills", FilterResult.Unknown, "Posting has no description, so required skills cannot be checked."));
                gaps.Add("Posting description is empty");
            }
            else
            {
                requiredFound = TextMatch.Found(criteria.RequiredSkills, corpus);
                checks.Add(requiredFound.Count == 0
                    ? new FilterCheck("requiredSkills", FilterResult.Fail, $"None of the required skills ({string.Join(", ", criteria.RequiredSkills)}) appear in the posting.")
                    : new FilterCheck("requiredSkills", FilterResult.Pass, "At least one required skill appears."));
            }
        }

        if (criteria.WorkModes.Count > 0)
        {
            if (workMode is null)
            {
                checks.Add(new FilterCheck("workModes", FilterResult.Unknown, "Work mode is not stated."));
                gaps.Add("Work mode not stated");
            }
            else checks.Add(criteria.WorkModes.Contains(workMode)
                ? new FilterCheck("workModes", FilterResult.Pass, $"{workMode} is an accepted work mode.")
                : new FilterCheck("workModes", FilterResult.Fail, $"Work mode {workMode} is not one you accept."));
        }

        var locationMatch = MatchLocation(criteria, input.Location, corpus, workMode);
        if (criteria.Locations.Count > 0)
        {
            if (locationMatch is not null)
                checks.Add(new FilterCheck("locations", FilterResult.Pass, $"Location matches {locationMatch}."));
            else if (input.Location is not null)
                checks.Add(new FilterCheck("locations", FilterResult.Fail, $"Location \"{input.Location}\" is not in your list."));
            else
            {
                checks.Add(new FilterCheck("locations", FilterResult.Unknown, "Location is not stated."));
                gaps.Add("Location not stated");
            }
        }

        // Scored criteria; each is applicable only when the user configured something for it.
        if (criteria.RequiredSkills.Count > 0)
            scores.Add(SkillScore(CampaignWeights.MandatorySkills, "Mandatory skills", criteria.RequiredSkills, hasText ? requiredFound : null, gaps, "Required skills"));
        if (criteria.PreferredSkills.Count > 0)
            scores.Add(SkillScore(CampaignWeights.PreferredSkills, "Preferred skills", criteria.PreferredSkills,
                hasText ? TextMatch.Found(criteria.PreferredSkills, corpus) : null, gaps, "Preferred skills"));
        if (criteria.CandidateYears is { } years)
            scores.Add(ExperienceScore(years, experience, gaps));
        if (criteria.Locations.Count > 0 || criteria.WorkModes.Count > 0)
            scores.Add(LocationScore(criteria, input.Location, workMode, locationMatch));

        var (score, coverage, rows) = Scoring.Combine(weights, scores, ev);
        var (outcome, reason) = Scoring.Outcome(checks);

        if (input.Organization.Length > 0) facts.Add(new FactRow("organization", "Company", input.Organization, ev, false));
        else gaps.Add("Company not stated");
        if (input.Location is not null) facts.Add(new FactRow("location", "Location", input.Location, ev, false));
        if (workMode is not null) facts.Add(new FactRow("workMode", "Work mode", workMode, ev, false));
        if (experience is not null) facts.Add(new FactRow("experience", "Experience asked", experience.ToString(), ev, false));
        if (requiredFound.Count > 0) facts.Add(new FactRow("requiredSkillsFound", "Required skills mentioned", string.Join(", ", requiredFound), ev, false));
        if (salary is not null) facts.Add(new FactRow("salary", "Salary", salary, ev, false));

        return new RuleResult(outcome, reason, score, coverage, rows, facts, gaps.Distinct().ToList(), checks);
    }

    /// <summary>
    /// Reads the first experience requirement in the text. Recognises "3-5 years", "3 to 5 yrs", "5+ years",
    /// "minimum 4 years" / "at least 4 years" and "4 years of experience" (read as a minimum).
    /// </summary>
    public static ExperienceRange? ParseExperience(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var range = RangePattern().Match(text);
        if (range.Success && Num(range.Groups["min"]) is { } lo && Num(range.Groups["max"]) is { } hi && lo <= hi && hi <= 50)
            return new ExperienceRange(lo, hi);
        var plus = PlusPattern().Match(text);
        if (plus.Success && Num(plus.Groups["min"]) is { } p && p <= 50) return new ExperienceRange(p, null);
        var minimum = MinimumPattern().Match(text);
        if (minimum.Success && Num(minimum.Groups["min"]) is { } m && m <= 50) return new ExperienceRange(m, null);
        var plain = PlainPattern().Match(text);
        if (plain.Success && Num(plain.Groups["min"]) is { } n && n <= 50) return new ExperienceRange(n, null);
        return null;
    }

    /// <summary>Remote, Hybrid or Onsite from keywords; null when the posting does not say. Hybrid wins over the others.</summary>
    public static string? DetectWorkMode(string? text, string? location = null)
    {
        var all = $"{location}\n{text}";
        if (HybridPattern().IsMatch(all)) return Hybrid;
        if (RemotePattern().IsMatch(all)) return Remote;
        if (OnsitePattern().IsMatch(all)) return Onsite;
        return null;
    }

    public static string? FindSalary(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = SalaryPattern().Match(text);
        if (m.Success) return m.Value.Trim();
        var lpa = LpaPattern().Match(text);
        return lpa.Success ? lpa.Value.Trim() : null;
    }

    private static CriterionScore SkillScore(string key, string label, IReadOnlyList<string> wanted, List<string>? found, List<string> gaps, string what)
    {
        if (found is null)
        {
            gaps.Add("Posting description is empty");
            return new CriterionScore(key, label, null, $"{what} cannot be checked: the posting has no description.");
        }
        var missing = wanted.Where(w => !found.Contains(w)).ToList();
        var value = Scoring.FractionValue(found.Count, wanted.Count);
        var reason = found.Count == wanted.Count
            ? $"All {wanted.Count} found: {string.Join(", ", found)}."
            : found.Count == 0
                ? $"None of {wanted.Count} found. Missing: {string.Join(", ", missing)}."
                : $"Found {found.Count} of {wanted.Count}: {string.Join(", ", found)}. Missing: {string.Join(", ", missing)}.";
        return new CriterionScore(key, label, value, reason);
    }

    private static CriterionScore ExperienceScore(double years, ExperienceRange? range, List<string> gaps)
    {
        const string label = "Experience";
        var y = years.ToString(CultureInfo.InvariantCulture);
        if (range is null)
        {
            gaps.Add("Experience requirement not stated");
            return new CriterionScore(CampaignWeights.Experience, label, null, "The posting does not state an experience range.");
        }
        var distance = years < range.Min ? range.Min - years : range.Max is { } max && years > max ? years - max : 0;
        return distance switch
        {
            0.0 => new CriterionScore(CampaignWeights.Experience, label, 1, $"Your {y} years are within the asked {range}."),
            <= 1.0 => new CriterionScore(CampaignWeights.Experience, label, 0.5, $"Your {y} years are within a year of the asked {range}."),
            _ => new CriterionScore(CampaignWeights.Experience, label, 0, $"Your {y} years are outside the asked {range}.")
        };
    }

    private static CriterionScore LocationScore(CampaignCriteria criteria, string? location, string? workMode, string? match)
    {
        const string label = "Location and work mode";
        if (criteria.WorkModes.Count > 0 && workMode is not null && !criteria.WorkModes.Contains(workMode))
            return new CriterionScore(CampaignWeights.Location, label, 0, $"Work mode {workMode} is not one you accept.");

        if (criteria.Locations.Count > 0)
        {
            if (match is not null) return new CriterionScore(CampaignWeights.Location, label, 1, $"Location matches {match}.");
            if (location is not null) return new CriterionScore(CampaignWeights.Location, label, 0, $"Location \"{location}\" is not in your list.");
            return new CriterionScore(CampaignWeights.Location, label, null, "The posting does not state a location.");
        }

        return workMode is null
            ? new CriterionScore(CampaignWeights.Location, label, null, "The posting does not state a work mode.")
            : new CriterionScore(CampaignWeights.Location, label, 1, $"Work mode {workMode} is accepted.");
    }

    /// <summary>The configured location that matches the location field or text; "Remote" also matches a remote posting.</summary>
    private static string? MatchLocation(CampaignCriteria criteria, string? location, string corpus, string? workMode) =>
        criteria.Locations.FirstOrDefault(l =>
            TextMatch.Contains(location, l) || TextMatch.Contains(corpus, l) ||
            (string.Equals(l, Remote, StringComparison.OrdinalIgnoreCase) && workMode == Remote));

    private static double? Num(Group g) =>
        g.Success && double.TryParse(g.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    private const string Years = @"(?:years?|yrs?)";

    [GeneratedRegex(@"(?<![\d.])(?<min>\d{1,2}(?:\.\d)?)\s*(?:-|–|—|to)\s*(?<max>\d{1,2}(?:\.\d)?)\s*\+?\s*" + Years + @"\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RangePattern();

    [GeneratedRegex(@"(?<![\d.])(?<min>\d{1,2}(?:\.\d)?)\s*\+\s*" + Years + @"\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PlusPattern();

    [GeneratedRegex(@"\b(?:minimum|min\.?|at\s+least|atleast)\s*(?:of\s+)?(?<min>\d{1,2}(?:\.\d)?)\s*\+?\s*" + Years + @"\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MinimumPattern();

    [GeneratedRegex(@"(?<![\d.])(?<min>\d{1,2}(?:\.\d)?)\s*" + Years + @"\s*(?:of\s+)?(?:\w+\s+)?(?:experience|exp)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PlainPattern();

    [GeneratedRegex(@"\bhybrid\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HybridPattern();

    [GeneratedRegex(@"\b(?:remote|work\s+from\s+home|wfh|work\s+from\s+anywhere)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RemotePattern();

    [GeneratedRegex(@"\b(?:on-?site|on\s+site|in-?office|in\s+office|work\s+from\s+office|wfo)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OnsitePattern();

    [GeneratedRegex(@"(?:₹|rs\.?|inr|\$|usd|€|eur|£|gbp)\s?\d[\d,]*(?:\.\d+)?\s?(?:(?:lpa|lakhs?|cr|k|l)\b)?(?:\s*(?:-|–|to)\s*(?:₹|rs\.?|inr|\$|usd|€|eur|£|gbp)?\s?\d[\d,]*(?:\.\d+)?\s?(?:(?:lpa|lakhs?|cr|k|l)\b)?)?(?:\s*(?:per\s+(?:year|annum|month)|/\s?(?:yr|year|month|mo)|p\.?a\.?))?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SalaryPattern();

    [GeneratedRegex(@"\b\d{1,3}(?:\.\d+)?\s*(?:-|–|to)\s*\d{1,3}(?:\.\d+)?\s*(?:lpa|lakhs?\s+per\s+annum)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LpaPattern();
}
