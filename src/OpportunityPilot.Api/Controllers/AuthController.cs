using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpportunityPilot.Api.Auth;
using OpportunityPilot.Application.Auth;

namespace OpportunityPilot.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(GuestAccess guests, GuestSessionService sessions) : ControllerBase
{
    public sealed record GuestSessionDto(string Token, DateTime ExpiresAt);

    /// <summary>A fresh random guest identity. 404 when guests are switched off (Auth:AllowGuests=false with accounts on).</summary>
    [HttpPost("guest")]
    [AllowAnonymous]
    public async Task<ActionResult<GuestSessionDto>> Guest(CancellationToken ct)
    {
        if (!guests.Enabled) return NotFound();
        var issued = await sessions.IssueAsync(ct);
        return new GuestSessionDto(issued.Token, issued.ExpiresAt);
    }
}
