using Microsoft.AspNetCore.Mvc;
using OpportunityPilot.Application.Research;

namespace OpportunityPilot.Api.Controllers;

[ApiController]
[Route("api/v1/research-jobs")]
public sealed class ResearchJobsController(ResearchService research) : ControllerBase
{
    /// <summary>Queues a run and returns at once; an already queued or running job for the campaign is returned instead.</summary>
    [HttpPost("/api/v1/campaigns/{campaignId:guid}/research")]
    public async Task<ActionResult<QueuedResearchDto>> Queue(Guid campaignId, CancellationToken ct) =>
        Accepted(await research.QueueAsync(campaignId, ct));

    [HttpGet("/api/v1/campaigns/{campaignId:guid}/research-jobs")]
    public async Task<ActionResult<IReadOnlyList<ResearchJobDto>>> List(Guid campaignId, CancellationToken ct) =>
        Ok(await research.ListAsync(campaignId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ResearchJobDto>> Get(Guid id, CancellationToken ct) =>
        await research.GetAsync(id, ct);

    /// <summary>A queued job is cancelled at once; a running one stops between items and keeps what it found.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ResearchJobDto>> Cancel(Guid id, CancellationToken ct) =>
        Accepted(await research.CancelAsync(id, ct));
}
