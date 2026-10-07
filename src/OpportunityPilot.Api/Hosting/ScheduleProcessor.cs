using OpportunityPilot.Application.Automation;

namespace OpportunityPilot.Api.Hosting;

public sealed class ScheduleProcessor(IServiceScopeFactory scopes, ILogger<ScheduleProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var queued = await scope.ServiceProvider.GetRequiredService<ScheduleService>().QueueDueAsync(stoppingToken);
                if (queued > 0) logger.LogInformation("Scheduled research queued {Count} campaign runs", queued);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogWarning(ex, "Schedule processor failed; retrying on the next poll"); }
            try { if (!await timer.WaitForNextTickAsync(stoppingToken)) return; }
            catch (OperationCanceledException) { return; }
        }
    }
}
