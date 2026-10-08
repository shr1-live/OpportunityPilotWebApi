using Microsoft.AspNetCore.Mvc;
using OpportunityPilot.Application.Imports;
using OpportunityPilot.Application.Sources;

namespace OpportunityPilot.Api.Controllers;

[ApiController]
[Route("api/v1/imports")]
public sealed class ImportsController(ImportService imports) : ControllerBase
{
    /// <summary>CSV text up to 1 MB and 1000 rows. Nothing is imported until the preview is committed.</summary>
    [HttpPost("preview")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(Hosting.RateLimits.Imports)]
    [RequestSizeLimit(3 * 1024 * 1024)] // the 1 MB CSV arrives JSON-escaped
    public async Task<ActionResult<ImportPreviewDto>> Preview(ImportPreviewRequest request, CancellationToken ct) =>
        await imports.PreviewAsync(request, ct);

    [HttpPost("{importId:guid}/commit")]
    public async Task<ActionResult<SourceDto>> Commit(Guid importId, [FromBody] CommitImportRequest? request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await imports.CommitAsync(importId, request, ct));
}
