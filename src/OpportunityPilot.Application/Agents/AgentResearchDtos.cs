using OpportunityPilot.Application.Campaigns;
using OpportunityPilot.Domain.Common;
using OpportunityPilot.Domain.Opportunities;

namespace OpportunityPilot.Application.Agents;

public sealed record AgentCampaignDto(Guid Id, string Name, OpportunityMode Mode, CampaignCriteria Criteria);

public sealed record AgentPostingItem(string ExternalId, string Url, string Title, string Company, string? Location, string? Description);

/// <param name="Platform">LinkedIn or Naukri: the site the agent read the postings from in the user's own browser.</param>
public sealed record AgentPostingsRequest(JobPlatform Platform, IReadOnlyList<AgentPostingItem> Items, bool QueueResearch);

/// <param name="Accepted">Distinct postings stored (duplicates in one batch count once).</param>
public sealed record AgentPostingsResult(int Accepted, Guid SourceId, Guid? JobId);

public sealed record AgentShortlistItem(
    Guid OpportunityId, Guid CampaignId, JobPlatform Platform, string ExternalId, string Url, string Title, string Organization);
