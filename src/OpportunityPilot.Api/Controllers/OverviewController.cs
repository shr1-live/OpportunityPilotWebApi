using Microsoft.AspNetCore.Mvc;
using OpportunityPilot.Application.Applications;
using OpportunityPilot.Application.Profiles;

namespace OpportunityPilot.Api.Controllers;

/// <summary>Counts for the Overview screen. Only entities that exist in this build are counted.</summary>
[ApiController]
[Route("api/v1/overview")]
public sealed class OverviewController(ProfileService profiles, ApplicationService applications) : ControllerBase
{
    public sealed record OverviewDto(int Profiles, int Applied, int NeedsManual);

    [HttpGet]
    public async Task<ActionResult<OverviewDto>> Get(CancellationToken ct)
    {
        var summary = await applications.SummaryAsync(ct);
        return new OverviewDto(await profiles.CountAsync(ct), summary.Applied, summary.NeedsManual);
    }
}
