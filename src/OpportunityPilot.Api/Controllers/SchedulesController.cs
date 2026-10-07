using Microsoft.AspNetCore.Mvc;
using OpportunityPilot.Application.Automation;

namespace OpportunityPilot.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class SchedulesController(ScheduleService schedules) : ControllerBase
{
    [HttpGet("schedules")]
    public async Task<ActionResult<IReadOnlyList<CampaignScheduleDto>>> List(CancellationToken ct) =>
        Ok(await schedules.ListAsync(ct));

    [HttpPut("campaigns/{campaignId:guid}/schedule")]
    public async Task<ActionResult<CampaignScheduleDto>> Upsert(
        Guid campaignId, UpsertCampaignScheduleRequest request, CancellationToken ct) =>
        await schedules.UpsertAsync(campaignId, request, ct);
}
