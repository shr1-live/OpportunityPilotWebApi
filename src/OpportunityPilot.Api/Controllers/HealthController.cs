using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OpportunityPilot.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("health")]
public sealed class HealthController(HealthCheckService health) : ControllerBase
{
    /// <summary>Process is up. No dependencies checked, no configuration returned.</summary>
    [HttpGet("live")]
    public IActionResult Live() => Ok(new { status = "live" });

    /// <summary>Database reachable. The response names the failing check but never connection details.</summary>
    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken ct)
    {
        var report = await health.CheckHealthAsync(ct);
        var body = new
        {
            status = report.Status == HealthStatus.Healthy ? "ready" : "not-ready",
            checks = report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString())
        };
        return report.Status == HealthStatus.Healthy ? Ok(body) : StatusCode(StatusCodes.Status503ServiceUnavailable, body);
    }
}
