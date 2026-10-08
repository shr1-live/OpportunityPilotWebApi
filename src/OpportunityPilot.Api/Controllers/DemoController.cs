using Microsoft.AspNetCore.Mvc;
using OpportunityPilot.Application.Demo;

namespace OpportunityPilot.Api.Controllers;

/// <summary>Replayable Sales + staffing demo: start (research queued), finish (after research), status, reset.</summary>
[ApiController]
[Route("api/v1/demo/sales")]
public sealed class DemoController(SalesDemoService demo) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SalesDemoStatus>> Status(CancellationToken ct) => Ok(await demo.StatusAsync(ct));

    [HttpPost]
    public async Task<ActionResult<SalesDemoStatus>> Start(CancellationToken ct) => Ok(await demo.StartAsync(ct));

    [HttpPost("finish")]
    public async Task<ActionResult<SalesDemoStatus>> Finish(CancellationToken ct) => Ok(await demo.FinishAsync(ct));

    [HttpDelete]
    public async Task<IActionResult> Reset(CancellationToken ct)
    {
        await demo.ResetAsync(ct);
        return NoContent();
    }
}
