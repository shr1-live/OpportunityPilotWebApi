using OpportunityPilot.Application.Common;
using OpportunityPilot.Application.Configuration;

namespace OpportunityPilot.Api.Hosting;

/// <summary>Runs retention a few minutes after start and then every 6 hours; each pass is bounded, and a full batch runs again soon.</summary>
public sealed class RetentionProcessor(IServiceScopeFactory scopes, SetupState setup, ILogger<RetentionProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!setup.DatabaseConfigured) return; // demo mode: data is in memory and goes away on restart anyway
        try { await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken); } catch (OperationCanceledException) { return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            var again = TimeSpan.FromHours(6);
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<RetentionService>().RunOnceAsync(stoppingToken);
                if (result.Total > 0)
                    logger.LogInformation("Retention removed {Imports} import previews, {Events} research events, {Ai} AI usage rows, {Security} security events, {Sessions} guest sessions",
                        result.ImportPreviews, result.ResearchEvents, result.AiUsage, result.SecurityEvents, result.GuestSessions);
                if (new[] { result.ImportPreviews, result.ResearchEvents, result.AiUsage, result.SecurityEvents, result.GuestSessions }.Any(n => n >= RetentionService.BatchSize))
                    again = TimeSpan.FromMinutes(5);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogWarning(ex, "Retention pass failed; retrying later"); }
            try { await Task.Delay(again, stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }
}
