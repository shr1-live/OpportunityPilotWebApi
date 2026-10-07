using System.Text.Json;
using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Application.Campaigns;

/// <summary>Per-mode criterion weights. Stored normalised: integers 0–100 that sum to exactly 100.</summary>
public static class CampaignWeights
{
    public const string MandatorySkills = "mandatorySkills";
    public const string Experience = "experience";
    public const string Location = "location";
    public const string PreferredSkills = "preferredSkills";

    public const string Industry = "industry";
    public const string Problem = "problem";
    public const string Geography = "geography";
    public const string Signal = "signal";
    public const string ContactPath = "contactPath";

    private static readonly IReadOnlyDictionary<string, int> JobDefaults = new Dictionary<string, int>
    {
        [MandatorySkills] = 40, [Experience] = 20, [Location] = 20, [PreferredSkills] = 20
    };

    private static readonly IReadOnlyDictionary<string, int> CustomerDefaults = new Dictionary<string, int>
    {
        [Industry] = 25, [Problem] = 30, [Geography] = 15, [Signal] = 20, [ContactPath] = 10
    };

    public static IReadOnlyDictionary<string, int> Defaults(OpportunityMode mode) => mode switch
    {
        OpportunityMode.Job => JobDefaults,
        OpportunityMode.Customer or OpportunityMode.Partner or OpportunityMode.Investor or OpportunityMode.Freelance => CustomerDefaults,
        _ => new Dictionary<string, int>()
    };

    /// <summary>
    /// Null input means the mode's defaults. Keys outside the mode are rejected; a key left out counts as 0.
    /// The result is scaled to sum to 100 with largest-remainder rounding, so it stays integer and exact.
    /// </summary>
    public static Dictionary<string, int> Normalise(
        OpportunityMode mode, IReadOnlyDictionary<string, double>? input, Dictionary<string, string[]> errors)
    {
        var defaults = Defaults(mode);
        if (input is null) return new Dictionary<string, int>(defaults);

        var raw = new Dictionary<string, double>();
        foreach (var key in defaults.Keys) raw[key] = 0;
        foreach (var (key, value) in input)
        {
            var known = defaults.Keys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
            if (known is null)
            {
                errors[$"weights.{key}"] = [$"Unknown criterion for {mode} campaigns. Use: {string.Join(", ", defaults.Keys)}."];
                continue;
            }
            if (double.IsNaN(value) || value < 0 || value > 100)
            {
                errors[$"weights.{known}"] = ["Each weight must be between 0 and 100."];
                continue;
            }
            raw[known] = value;
        }

        var total = raw.Values.Sum();
        if (total <= 0)
        {
            if (!errors.Keys.Any(k => k.StartsWith("weights", StringComparison.Ordinal)))
                errors["weights"] = ["At least one weight must be above 0."];
            return new Dictionary<string, int>(defaults);
        }

        var scaled = raw.ToDictionary(kv => kv.Key, kv => kv.Value * 100 / total);
        var result = scaled.ToDictionary(kv => kv.Key, kv => (int)Math.Floor(kv.Value));
        var remainder = 100 - result.Values.Sum();
        foreach (var key in scaled.OrderByDescending(kv => kv.Value - Math.Floor(kv.Value)).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                     .Select(kv => kv.Key).Take(remainder))
            result[key]++;
        return result;
    }

    public static string ToJson(IReadOnlyDictionary<string, int> weights) => JsonSerializer.Serialize(weights);

    /// <summary>Unknown or broken JSON falls back to the defaults rather than scoring with nothing.</summary>
    public static IReadOnlyDictionary<string, int> FromJson(OpportunityMode mode, string? json)
    {
        var defaults = Defaults(mode);
        try
        {
            var stored = string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<Dictionary<string, int>>(json);
            if (stored is null || stored.Count == 0) return defaults;
            return defaults.Keys.ToDictionary(k => k, k => stored.GetValueOrDefault(k));
        }
        catch (JsonException)
        {
            return defaults;
        }
    }
}
