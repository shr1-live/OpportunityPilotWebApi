using Microsoft.AspNetCore.Mvc;
using OpportunityPilot.Application.Ai;

namespace OpportunityPilot.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class AiController(AiService ai) : ControllerBase
{
    [HttpPost("goal-previews")]
    public async Task<ActionResult<GoalPreviewDto>> Preview(GoalPreviewRequest request, CancellationToken ct) => await ai.PreviewGoalAsync(request, ct);

    [HttpGet("ai/status")]
    public ActionResult<AiStatusDto> Status() => ai.Status();
}
