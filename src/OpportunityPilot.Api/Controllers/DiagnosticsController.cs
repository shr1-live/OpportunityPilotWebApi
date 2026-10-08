using Microsoft.AspNetCore.Mvc;
using OpportunityPilot.Application.Common;

namespace OpportunityPilot.Api.Controllers;

/// <summary>
/// Deployment check for any signed-in user: version, environment, database reachability, pending migrations, the
/// schema-exposure check and this instance's operation counters. Contains no secrets, hosts, keys or owner data.
/// </summary>
[ApiController]
[Route("api/v1/diagnostics")]
public sealed class DiagnosticsController(IDatabaseDiagnostics database, OperationalMetrics metrics, IWebHostEnvironment environment, TimeProvider clock) : ControllerBase
{
    /// <summary>Render sets RENDER_GIT_COMMIT; elsewhere the assembly version is shown.</summary>
    private static readonly string Version = Environment.GetEnvironmentVariable("RENDER_GIT_COMMIT") is { Length: >= 7 } sha
        ? sha[..7]
        : typeof(DiagnosticsController).Assembly.GetName().Version?.ToString() ?? "unknown";

    [HttpGet]
    public async Task<ActionResult<DiagnosticsDto>> Get(CancellationToken ct) =>
        Ok(new DiagnosticsDto(Version, environment.EnvironmentName, metrics.StartedAt, clock.GetUtcNow().UtcDateTime,
            await database.CheckAsync(ct), metrics.Snapshot()));
}
