using Microsoft.AspNetCore.Mvc;
using OpportunityPilot.Application.Wellfound;
using OpportunityPilot.Domain.Wellfound;

namespace OpportunityPilot.Api.Controllers;

[ApiController]
[Route("api/v1/wellfound")]
public sealed class WellfoundController(WellfoundService service) : ControllerBase
{
    [HttpGet("status")]
    public ActionResult<WellfoundStatusDto> Status() => Ok(service.Status());

    [HttpGet("jobs")]
    public async Task<ActionResult<IReadOnlyList<WellfoundJobDto>>> Jobs(
        [FromQuery] string workspace = "Candidate", [FromQuery] string? keyword = null,
        [FromQuery] string? location = null, [FromQuery] bool remoteOnly = false,
        [FromQuery] decimal? minSalary = null, [FromQuery] bool equityOnly = false,
        [FromQuery] string? fundingStage = null, [FromQuery] int take = 100,
        CancellationToken ct = default) => Ok(await service.JobsAsync(workspace, keyword, location, remoteOnly,
            minSalary, equityOnly, fundingStage, take, ct));

    [HttpPatch("jobs/{id:guid}/state")]
    public async Task<ActionResult<WellfoundJobDto>> ChangeJobState(
        Guid id, ChangeWellfoundJobStateRequest request, CancellationToken ct) =>
        Ok(await service.ChangeJobStateAsync(id, request, ct));

    [HttpGet("applications")]
    public async Task<ActionResult<IReadOnlyList<WellfoundApplicationDto>>> Applications(
        [FromQuery] WellfoundApplicationState? state = null, CancellationToken ct = default) =>
        Ok(await service.ApplicationsAsync(state, ct));

    [HttpPatch("applications/{id:guid}/state")]
    public async Task<ActionResult<WellfoundApplicationDto>> ChangeApplicationState(
        Guid id, ChangeWellfoundApplicationStateRequest request, CancellationToken ct) =>
        Ok(await service.ChangeApplicationStateAsync(id, request, ct));

    [HttpGet("activities")]
    public async Task<ActionResult<IReadOnlyList<WellfoundActivityDto>>> Activities(
        [FromQuery] int take = 50, CancellationToken ct = default) => Ok(await service.ActivitiesAsync(take, ct));

    [HttpGet("kpis")]
    public async Task<ActionResult<WellfoundKpisDto>> Kpis(
        [FromQuery] string workspace = "Candidate", CancellationToken ct = default) =>
        Ok(await service.KpisAsync(workspace, ct));

    [HttpPost("demo/load")]
    public async Task<ActionResult<LoadWellfoundDemoResult>> LoadDemo(CancellationToken ct) =>
        Ok(await service.LoadDemoAsync(ct));

    [HttpPost("public/sync")]
    public async Task<ActionResult<SyncWellfoundPublicResult>> SyncPublic(CancellationToken ct) =>
        Ok(await service.SyncPublicAsync(ct));
}
