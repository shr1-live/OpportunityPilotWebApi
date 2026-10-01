using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpportunityPilot.Api.Auth;
using OpportunityPilot.Application.Agents;
using OpportunityPilot.Domain.Opportunities;

namespace OpportunityPilot.Api.Controllers;

/// <summary>
/// The desktop agent's research endpoints. Agent key only: user tokens are rejected here, and the key works
/// nowhere else (it is not part of the fallback policy).
/// </summary>
[ApiController]
[Route("api/v1/agent")]
[Authorize(AuthenticationSchemes = AgentKeyAuthenticationHandler.SchemeName)]
public sealed class AgentResearchController(AgentResearchService agent) : ControllerBase
{
    /// <summary>The owner's Job campaigns, with the criteria the agent searches with.</summary>
    [HttpGet("campaigns")]
    public async Task<ActionResult<IReadOnlyList<AgentCampaignDto>>> Campaigns(CancellationToken ct) =>
        Ok(await agent.CampaignsAsync(ct));

    [HttpPost("campaigns/{id:guid}/postings")]
    [RequestSizeLimit(4 * 1024 * 1024)]
    public async Task<ActionResult<AgentPostingsResult>> Postings(Guid id, AgentPostingsRequest request, CancellationToken ct) =>
        await agent.DeliverAsync(id, request, ct);

    /// <summary>Shortlisted Job opportunities on the platform that have not been applied to.</summary>
    [HttpGet("shortlist")]
    public async Task<ActionResult<IReadOnlyList<AgentShortlistItem>>> Shortlist([FromQuery] JobPlatform? platform, CancellationToken ct) =>
        Ok(await agent.ShortlistAsync(platform, ct));
}
