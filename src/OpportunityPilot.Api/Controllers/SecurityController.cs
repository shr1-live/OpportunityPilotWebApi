using Microsoft.AspNetCore.Mvc;
using OpportunityPilot.Application.Auth;

namespace OpportunityPilot.Api.Controllers;

/// <summary>The signed-in owner's own security events (keys created/revoked, data exported/deleted, guest sessions).</summary>
[ApiController]
[Route("api/v1/security")]
public sealed class SecurityController(SecurityAudit audit) : ControllerBase
{
    [HttpGet("events")]
    public async Task<ActionResult<IReadOnlyList<SecurityEventDto>>> Events([FromQuery] int take = 50, CancellationToken ct = default) =>
        Ok(await audit.ListAsync(take, ct));
}
