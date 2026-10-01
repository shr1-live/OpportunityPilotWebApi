using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpportunityPilot.Application.Capabilities;

namespace OpportunityPilot.Api.Controllers;

[ApiController]
[Route("api/v1/capabilities")]
public sealed class CapabilitiesController(CapabilityService capabilities, IWebHostEnvironment env) : ControllerBase
{
    /// <summary>What this deployment can actually do. Contains no secrets; readable before sign-in.</summary>
    [HttpGet]
    [AllowAnonymous]
    public ActionResult<CapabilitiesDto> Get() => capabilities.Get(env.EnvironmentName);
}
