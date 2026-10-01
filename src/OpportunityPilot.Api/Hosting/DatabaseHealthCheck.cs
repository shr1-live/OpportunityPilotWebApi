using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpportunityPilot.Application.Configuration;
using OpportunityPilot.Infrastructure.Persistence;

namespace OpportunityPilot.Api.Hosting;

public sealed class DatabaseHealthCheck(SetupState setup, IServiceProvider services) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        if (!setup.DatabaseConfigured) return HealthCheckResult.Unhealthy("Database not configured.");
        try
        {
            return await services.GetRequiredService<AppDbContext>().Database.CanConnectAsync(ct)
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
