using Microsoft.AspNetCore.Mvc;
using OpportunityPilot.Application.Sales;
using OpportunityPilot.Domain.Sales;

namespace OpportunityPilot.Api.Controllers;

[ApiController]
[Route("api/v1/sales/upwork-opportunities")]
public sealed class UpworkOpportunitiesController(UpworkOpportunityService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UpworkOpportunityDto>>> List(
        [FromQuery] UpworkOpportunityState? state = null, [FromQuery] int take = 100,
        CancellationToken ct = default) => Ok(await service.ListAsync(state, take, ct));

    [HttpPost("import")]
    public async Task<ActionResult<UpworkOpportunityDto>> Import(ImportUpworkOpportunityRequest request, CancellationToken ct) =>
        Ok(await service.ImportAsync(request, ct));

    [HttpPatch("{id:guid}/decision")]
    public async Task<ActionResult<UpworkOpportunityDto>> Decide(Guid id, DecideUpworkOpportunityRequest request, CancellationToken ct) =>
        Ok(await service.DecideAsync(id, request, ct));

    [HttpPost("{id:guid}/promote")]
    public async Task<ActionResult<SalesProjectDto>> Promote(Guid id, CancellationToken ct) =>
        Ok(await service.PromoteAsync(id, ct));
}
