using Microsoft.AspNetCore.Mvc;
using OpportunityPilot.Application.Outreach;

namespace OpportunityPilot.Api.Controllers;

/// <summary>Provider executions: start (approval, suppression, quota, idempotency), confirm with a receipt, or record a failure.</summary>
[ApiController]
[Route("api/v1/executions")]
public sealed class ExecutionsController(ProviderGateway gateway) : ControllerBase
{
    [HttpPost("drafts/{draftId:guid}")]
    public async Task<ActionResult<ProviderExecutionDto>> StartDraft(Guid draftId, StartExecutionRequest request, CancellationToken ct) =>
        Ok(await gateway.StartDraftAsync(draftId, request, ct));

    [HttpPost("bids/{bidId:guid}")]
    public async Task<ActionResult<ProviderExecutionDto>> StartBid(Guid bidId, StartExecutionRequest request, CancellationToken ct) =>
        Ok(await gateway.StartBidAsync(bidId, request, ct));

    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<ProviderExecutionDto>> Confirm(Guid id, ConfirmExecutionRequest request, CancellationToken ct) =>
        Ok(await gateway.ConfirmAsync(id, request, ct));

    [HttpPost("{id:guid}/fail")]
    public async Task<ActionResult<ProviderExecutionDto>> Fail(Guid id, FailExecutionRequest request, CancellationToken ct) =>
        Ok(await gateway.FailAsync(id, request, ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProviderExecutionDto>>> List([FromQuery] Guid subjectId, CancellationToken ct) =>
        Ok(await gateway.ListAsync(subjectId, ct));
}
