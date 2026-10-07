using System.Text.Json;
using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Application.Campaigns;

/// <summary>Every list may be empty, meaning "not applied". Stored as JSON on the campaign.</summary>
public sealed record CampaignCriteria
{
    public const int MaxItemsPerList = 50;
    public const int MaxItemLength = 100;
    public const double MaxCandidateYears = 60;
    public const int MinPostingAge = 1;
    public const int MaxPostingAge = 365;
    public static readonly string[] AllowedWorkModes = ["Remote", "Hybrid", "Onsite"];
    public static readonly CampaignCriteria Empty = new();

    /// <summary>Job: titles/search phrases; Customer: product/search words. Used by the agent's searches.</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];
    public IReadOnlyList<string> RequiredSkills { get; init; } = [];
    public IReadOnlyList<string> PreferredSkills { get; init; } = [];

    /// <summary>The user's confirmed years of experience.</summary>
    public double? CandidateYears { get; init; }

    public IReadOnlyList<string> Locations { get; init; } = [];
    public IReadOnlyList<string> WorkModes { get; init; } = [];
    public IReadOnlyList<string> Industries { get; init; } = [];
    public IReadOnlyList<string> Problems { get; init; } = [];
    public IReadOnlyList<string> Signals { get; init; } = [];
    public IReadOnlyList<string> ExcludeKeywords { get; init; } = [];
    public IReadOnlyList<string> ExcludeOrganizations { get; init; } = [];

    /// <summary>Job: exclude postings that look like a staffing agency (body phrase or company-name pattern). Off by default.</summary>
    public bool ExcludeStaffingAgencies { get; init; }

    /// <summary>Job: exclude postings the source dates older than this many days (1–365); null = off. Undated postings are kept.</summary>
    public int? MaxPostingAgeDays { get; init; }

    public string ToJson() => JsonSerializer.Serialize(this, JsonSerializerOptions.Web);

    public static CampaignCriteria FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Empty;
        try { return Normalise(JsonSerializer.Deserialize<CampaignCriteria>(json, JsonSerializerOptions.Web), []); }
        catch (JsonException) { return Empty; }
    }

    /// <summary>Trims, drops blanks and case-insensitive duplicates, canonicalises work modes; collects field errors.</summary>
    public static CampaignCriteria Normalise(CampaignCriteria? input, Dictionary<string, string[]> errors)
    {
        input ??= Empty;
        var canonical = new List<string>();
        foreach (var mode in List(input.WorkModes, "criteria.workModes", errors))
        {
            var bare = mode.Replace("-", "").Replace(" ", "");
            var match = AllowedWorkModes.FirstOrDefault(m => string.Equals(m, bare, StringComparison.OrdinalIgnoreCase));
            if (match is null) errors["criteria.workModes"] = ["Work modes must be Remote, Hybrid or Onsite."];
            else if (!canonical.Contains(match)) canonical.Add(match);
        }

        if (input.CandidateYears is { } years && (double.IsNaN(years) || years < 0 || years > MaxCandidateYears))
            errors["criteria.candidateYears"] = [$"Years of experience must be between 0 and {MaxCandidateYears}."];

        var ageOk = input.MaxPostingAgeDays is not { } age || age is >= MinPostingAge and <= MaxPostingAge;
        if (!ageOk) errors["criteria.maxPostingAgeDays"] = [$"Maximum posting age must be between {MinPostingAge} and {MaxPostingAge} days."];

        return new CampaignCriteria
        {
            Keywords = List(input.Keywords, "criteria.keywords", errors),
            RequiredSkills = List(input.RequiredSkills, "criteria.requiredSkills", errors),
            PreferredSkills = List(input.PreferredSkills, "criteria.preferredSkills", errors),
            CandidateYears = input.CandidateYears,
            Locations = List(input.Locations, "criteria.locations", errors),
            WorkModes = canonical,
            Industries = List(input.Industries, "criteria.industries", errors),
            Problems = List(input.Problems, "criteria.problems", errors),
            Signals = List(input.Signals, "criteria.signals", errors),
            ExcludeKeywords = List(input.ExcludeKeywords, "criteria.excludeKeywords", errors),
            ExcludeOrganizations = List(input.ExcludeOrganizations, "criteria.excludeOrganizations", errors),
            ExcludeStaffingAgencies = input.ExcludeStaffingAgencies,
            MaxPostingAgeDays = ageOk ? input.MaxPostingAgeDays : null
        };
    }

    public static bool IsSupportedMode(OpportunityMode mode) => Enum.IsDefined(mode);

    private static List<string> List(IReadOnlyList<string>? values, string key, Dictionary<string, string[]> errors)
    {
        var result = new List<string>();
        foreach (var raw in values ?? [])
        {
            var value = raw?.Trim();
            if (string.IsNullOrEmpty(value)) continue;
            if (value.Length > MaxItemLength)
            {
                errors[key] = [$"Each entry must be at most {MaxItemLength} characters."];
                continue;
            }
            if (!result.Contains(value, StringComparer.OrdinalIgnoreCase)) result.Add(value);
        }
        if (result.Count > MaxItemsPerList) errors[key] = [$"At most {MaxItemsPerList} entries."];
        return result;
    }
}
