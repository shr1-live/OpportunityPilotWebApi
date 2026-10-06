using Microsoft.AspNetCore.Mvc;
using OpportunityPilot.Application.Sales;
using OpportunityPilot.Domain.Sales;

namespace OpportunityPilot.Api.Controllers;

[ApiController]
[Route("api/v1/sales")]
public sealed class SalesController(SalesService sales) : ControllerBase
{
    [HttpGet("projects")]
    public async Task<ActionResult<IReadOnlyList<SalesProjectDto>>> List(
        [FromQuery] int take = 50, [FromQuery] int skip = 0, [FromQuery] SalesProjectState? state = null,
        [FromQuery] SalesProjectSource? source = null, CancellationToken ct = default) =>
        Ok(await sales.ListProjectsAsync(take, skip, state, source, ct));

    [HttpPost("projects")]
    public async Task<ActionResult<SalesProjectDto>> Create(CreateSalesProjectRequest request, CancellationToken ct)
    {
        var created = await sales.CreateProjectAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet("projects/{id:guid}")]
    public async Task<ActionResult<SalesProjectDto>> Get(Guid id, CancellationToken ct) => Ok(await sales.GetProjectAsync(id, ct));

    [HttpPost("projects/{id:guid}/bid")]
    public async Task<ActionResult<SalesProjectDto>> CreateBid(Guid id, CreateSalesBidRequest request, CancellationToken ct) =>
        Ok(await sales.CreateBidAsync(id, request, ct));

    [HttpPut("bids/{id:guid}")]
    public async Task<ActionResult<SalesProjectDto>> UpdateBid(Guid id, UpdateSalesBidRequest request, CancellationToken ct) =>
        Ok(await sales.UpdateBidAsync(id, request, ct));

    [HttpPost("bids/{id:guid}/approve")]
    public async Task<ActionResult<SalesProjectDto>> ApproveBid(Guid id, ApproveSalesBidRequest request, CancellationToken ct) =>
        Ok(await sales.ApproveBidAsync(id, request, ct));
}
