using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpportunityPilot.Infrastructure.Persistence;

namespace OpportunityPilot.Api.Hosting;

public sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(ct)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Database unreachable.");
        }
        catch (Exception)
        {
            // No connection details in the response.
            return HealthCheckResult.Unhealthy("Database unreachable.");
        }
    }
}
