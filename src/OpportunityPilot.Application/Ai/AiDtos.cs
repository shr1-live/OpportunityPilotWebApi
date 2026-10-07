using OpportunityPilot.Application.Campaigns;
using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Application.Ai;

public enum AiSource { Gemini, Rules, Template }
public sealed record GoalPreviewRequest(Guid ProfileId, string Goal, OpportunityMode? Mode);
public sealed record GoalPreviewDto(AiSource Source, string? FallbackReason, OpportunityMode? Mode,
    CampaignCriteria Criteria, IReadOnlyList<string> Ambiguities, IReadOnlyList<string> Notes);
public sealed record AiStatusDto(bool Active, string Provider, string Model, string? FallbackReason,
    int CallsToday, int DailyLimit, int MaxCallsPerRun, object? LastFailure);

public interface ILlmClient
{
    Task<string?> GenerateJsonAsync(string operation, string system, string input, CancellationToken ct);
}
