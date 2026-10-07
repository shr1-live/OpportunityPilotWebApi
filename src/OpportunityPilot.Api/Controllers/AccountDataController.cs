using Microsoft.AspNetCore.Mvc;
using OpportunityPilot.Application.Accounts;

namespace OpportunityPilot.Api.Controllers;

[ApiController]
[Route("api/v1/account-data")]
public sealed class AccountDataController(AccountDataService accountData) : ControllerBase
{
    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct) => Ok(await accountData.ExportAsync(ct));

    [HttpDelete]
    public async Task<IActionResult> Delete(DeleteAccountDataRequest request, CancellationToken ct)
    {
        await accountData.DeleteAsync(request, ct);
        return NoContent();
    }
}
