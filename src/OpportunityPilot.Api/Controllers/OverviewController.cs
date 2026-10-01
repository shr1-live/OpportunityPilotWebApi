using Microsoft.AspNetCore.Mvc;
using OpportunityPilot.Application.Profiles;

namespace OpportunityPilot.Api.Controllers;

/// <summary>Counts for the Overview screen. Only entities that exist in this build are counted.</summary>
[ApiController]
[Route("api/v1/overview")]
public sealed class OverviewController(ProfileService profiles) : ControllerBase
{
    public sealed record OverviewDto(int Profiles);

    [HttpGet]
    public async Task<ActionResult<OverviewDto>> Get(CancellationToken ct) =>
        new OverviewDto(await profiles.CountAsync(ct));
}
