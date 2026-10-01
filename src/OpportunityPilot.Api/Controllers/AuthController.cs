using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpportunityPilot.Api.Auth;

namespace OpportunityPilot.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(GuestTokens guests, TimeProvider clock) : ControllerBase
{
    public sealed record GuestSessionDto(string Token, DateTime ExpiresAt);

    /// <summary>Demo mode only: a fresh random guest identity. 404 once real sign-in is configured.</summary>
    [HttpPost("guest")]
    [AllowAnonymous]
    public ActionResult<GuestSessionDto> Guest()
    {
        if (!guests.Enabled) return NotFound();
        var (token, expiresAt) = guests.Issue(clock.GetUtcNow().UtcDateTime);
        return new GuestSessionDto(token, expiresAt);
    }
}
