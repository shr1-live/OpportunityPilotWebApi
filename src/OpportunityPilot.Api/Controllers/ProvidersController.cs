using Microsoft.AspNetCore.Mvc;
using OpportunityPilot.Application.Capabilities;

namespace OpportunityPilot.Api.Controllers;

/// <summary>Provider readiness and compliance matrix for the signed-in owner (P7).</summary>
[ApiController]
[Route("api/v1/providers")]
public sealed class ProvidersController(ProviderReadinessService readiness) : ControllerBase
{
    [HttpGet("readiness")]
    public async Task<ActionResult<IReadOnlyList<ProviderReadinessDto>>> Readiness(CancellationToken ct) => Ok(await readiness.ListAsync(ct));
}
