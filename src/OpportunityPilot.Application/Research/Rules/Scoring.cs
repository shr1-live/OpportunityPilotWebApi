using OpportunityPilot.Domain.Opportunities;

namespace OpportunityPilot.Application.Research.Rules;

/// <summary>
/// Plan §12 arithmetic. Only criteria the user configured are passed in (the rest are not applicable and their
/// weight is redistributed proportionally). Unknown (null) criteria stay in: they score 0 and lower coverage,
/// so a sparse source cannot look like a strong match.
/// </summary>
public static class Scoring
{
    public static (int Score, int Coverage, List<BreakdownRow> Rows) Combine(
        IReadOnlyDictionary<string, int> weights, IReadOnlyList<CriterionScore> applicable, string? evidenceId)
    {
        var weighted = applicable.Select(c => (Criterion: c, Weight: (double)Math.Max(0, weights.GetValueOrDefault(c.Criterion))))
            .Where(x => x.Weight > 0).ToList();
        var total = weighted.Sum(x => x.Weight);
        if (total <= 0) return (0, 0, []);

        double score = 0, known = 0;
        var rows = new List<BreakdownRow>();
        foreach (var (c, w) in weighted)
        {
            var effective = w * 100 / total;
            var points = effective * (c.Value ?? 0);
            score += points;
            if (c.Value is not null) known += effective;
            IReadOnlyList<string> ids = c.Value is not null && evidenceId is not null ? [evidenceId] : [];
            rows.Add(new BreakdownRow(c.Criterion, c.Label, Math.Round(effective, 2), c.Value, Math.Round(points, 2), c.Reason, ids,
                c.Value is null ? null : Excerpts.Bound(c.Excerpt)));
        }
        return (Clamp(score), Clamp(known), rows);
    }

    /// <summary>Any Fail excludes; otherwise any Unknown needs verification; otherwise qualified.</summary>
    public static (FilterOutcome Outcome, string Reason) Outcome(IReadOnlyList<FilterCheck> checks)
    {
        var fails = checks.Where(c => c.Result == FilterResult.Fail).ToList();
        if (fails.Count > 0) return (FilterOutcome.Excluded, string.Join(" ", fails.Select(f => f.Reason)));
        var unknown = checks.Where(c => c.Result == FilterResult.Unknown).ToList();
        if (unknown.Count > 0) return (FilterOutcome.NeedsVerification, string.Join(" ", unknown.Select(f => f.Reason)));
        return (FilterOutcome.Qualified, checks.Count == 0 ? "No hard filters configured." : "Passed every configured hard filter.");
    }

    /// <summary>All → 1; at least half → 0.5; less → 0.</summary>
    public static double FractionValue(int found, int of) =>
        of == 0 ? 0 : found == of ? 1 : found * 2 >= of ? 0.5 : 0;

    private static int Clamp(double value) => (int)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 100);
}
