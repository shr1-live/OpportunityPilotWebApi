namespace OpportunityPilot.Application.Automation;

public sealed record CampaignScheduleDto(
    Guid Id, Guid CampaignId, string CampaignName, string TimeZone, int CadenceMinutes, DateTime NextRunAt,
    bool Paused, DateTime? LastQueuedAt, string? LastSafeError, int Version, DateTime UpdatedAt);

public sealed record UpsertCampaignScheduleRequest(
    string TimeZone, int CadenceMinutes, DateTime NextRunAt, bool Paused, int? ExpectedVersion);
