using Microsoft.AspNetCore.Mvc;
using OpportunityPilot.Application.Analytics;

namespace OpportunityPilot.Api.Controllers;

/// <summary>Overview metrics for the v2 Candidate and Sales overviews (docs/ANALYTICS_CONTRACT.md). User auth only.</summary>
[ApiController]
[Route("api/v1/analytics")]
public sealed class AnalyticsController(AnalyticsService analytics) : ControllerBase
{
    /// <summary>workspace Candidate (Job campaigns) or Sales (Customer campaigns); days 1–365 (default 30).</summary>
    [HttpGet("overview")]
    public async Task<ActionResult<AnalyticsOverviewDto>> Overview(
        [FromQuery] string? workspace, [FromQuery] int days = AnalyticsService.DefaultDays, CancellationToken ct = default) =>
        await analytics.OverviewAsync(workspace, days, ct);
}
