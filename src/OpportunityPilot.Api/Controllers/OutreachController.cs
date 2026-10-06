using Microsoft.AspNetCore.Mvc;
using OpportunityPilot.Application.Opportunities;
using OpportunityPilot.Application.Outreach;
using OpportunityPilot.Domain.Outreach;

namespace OpportunityPilot.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class OutreachController(OutreachService outreach) : ControllerBase
{
    [HttpGet("suppressions")]
    public async Task<ActionResult<IReadOnlyList<SuppressionDto>>> Suppressions(CancellationToken ct) => Ok(await outreach.ListSuppressionsAsync(ct));
    [HttpPost("suppressions")]
    public async Task<ActionResult<SuppressionDto>> Suppress(CreateSuppressionRequest request, CancellationToken ct)
    {
        var row = await outreach.CreateSuppressionAsync(request, ct);
        return Created($"/api/v1/suppressions/{row.Id}", row);
    }
    [HttpDelete("suppressions/{id:guid}")]
    public async Task<IActionResult> Unsuppress(Guid id, CancellationToken ct) { await outreach.DeleteSuppressionAsync(id, ct); return NoContent(); }

    [HttpGet("next-actions")]
    public async Task<ActionResult<IReadOnlyList<NextActionDto>>> NextActions([FromQuery] NextActionState? state, CancellationToken ct) => Ok(await outreach.ListNextActionsAsync(state, ct));
    [HttpGet("opportunities/{opportunityId:guid}/next-actions")]
    public async Task<ActionResult<IReadOnlyList<NextActionDto>>> OpportunityNextActions(Guid opportunityId, CancellationToken ct) => Ok(await outreach.ListForOpportunityAsync(opportunityId, ct));
    [HttpPost("opportunities/{opportunityId:guid}/next-actions")]
    public async Task<ActionResult<NextActionDto>> CreateNextAction(Guid opportunityId, CreateNextActionRequest request, CancellationToken ct)
    {
        var row = await outreach.CreateNextActionAsync(opportunityId, request, ct);
        return Created($"/api/v1/next-actions/{row.Id}", row);
    }
    [HttpPatch("next-actions/{id:guid}")]
    public async Task<ActionResult<NextActionDto>> UpdateNextAction(Guid id, UpdateNextActionRequest request, CancellationToken ct) => await outreach.UpdateNextActionAsync(id, request, ct);

    [HttpGet("opportunities/{opportunityId:guid}/activities")]
    public async Task<ActionResult<IReadOnlyList<ActivityDto>>> Activities(Guid opportunityId, CancellationToken ct) => Ok(await outreach.ListActivitiesAsync(opportunityId, ct));
    [HttpPost("opportunities/{opportunityId:guid}/activities")]
    public async Task<ActionResult<ActivityDto>> CreateActivity(Guid opportunityId, CreateActivityRequest request, CancellationToken ct)
    {
        var row = await outreach.CreateActivityAsync(opportunityId, request, ct);
        return Created($"/api/v1/opportunities/{opportunityId}/activities", row);
    }
}
