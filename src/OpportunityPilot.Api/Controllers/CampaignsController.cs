using Microsoft.AspNetCore.Mvc;
using OpportunityPilot.Application.Campaigns;

namespace OpportunityPilot.Api.Controllers;

[ApiController]
[Route("api/v1/campaigns")]
public sealed class CampaignsController(CampaignService campaigns) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CampaignSummaryDto>>> List(CancellationToken ct) =>
        Ok(await campaigns.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CampaignDto>> Get(Guid id, CancellationToken ct) =>
        await campaigns.GetAsync(id, ct);

    /// <summary>Job and Customer only for now; other modes return 400 "not supported yet".</summary>
    [HttpPost]
    public async Task<ActionResult<CampaignDto>> Create(CreateCampaignRequest request, CancellationToken ct)
    {
        var created = await campaigns.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CampaignDto>> Update(Guid id, UpdateCampaignRequest request, CancellationToken ct) =>
        await campaigns.UpdateAsync(id, request, ct);
}
