using OpportunityPilot.Domain.Opportunities;

namespace OpportunityPilot.Application.Research.Rules;

/// <summary>What the rules may look at for one candidate. Everything comes from the source; nothing is guessed.</summary>
/// <param name="Text">The posting or company description (bounded). Empty means the source gave no text.</param>
/// <param name="EvidenceId">The evidence row the text was stored in; every known value cites it.</param>
/// <param name="Links">Absolute links found on a fetched page (contact, careers, mailto).</param>
public sealed record RuleInput(
    string Title,
    string Organization,
    string? Location,
    string Text,
    string? EvidenceId,
    string? Website = null,
    string? Country = null,
    string? Industry = null,
    IReadOnlyList<string>? Links = null);

public enum FilterResult
{
    Pass,
    Fail,
    Unknown
}

public sealed record FilterCheck(string Filter, FilterResult Result, string Reason);

/// <param name="Value">1, 0.5, 0, or null when the source does not say (unknown is not the same as 0, but scores 0).</param>
public sealed record CriterionScore(string Criterion, string Label, double? Value, string Reason);

/// <param name="Weight">Effective weight after redistributing not-applicable criteria (sums to 100 over the rows).</param>
public sealed record BreakdownRow(
    string Criterion, string Label, double Weight, double? Value, double Points, string Reason, IReadOnlyList<string> EvidenceIds);

/// <param name="IsInference">True when the value is derived rather than stated by the source.</param>
public sealed record FactRow(string Key, string Label, string Value, string? EvidenceId, bool IsInference);

public sealed record RuleResult(
    FilterOutcome Outcome,
    string? OutcomeReason,
    int Score,
    int Coverage,
    IReadOnlyList<BreakdownRow> Breakdown,
    IReadOnlyList<FactRow> Facts,
    IReadOnlyList<string> Gaps,
    IReadOnlyList<FilterCheck> Filters);
