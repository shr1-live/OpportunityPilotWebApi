using Microsoft.Extensions.Options;
using OpportunityPilot.Application.Research;

namespace OpportunityPilot.Api.Hosting;

/// <summary>
/// The single in-process research worker. Polls for a claimable job every couple of seconds and runs it through
/// <see cref="IResearchRunner"/>. Durable state lives in the database, so a restart (or a sleeping free instance)
/// resumes from the job's lease rather than losing it. This is not a 24/7 scheduler: it runs only while the API runs.
/// </summary>
public sealed class ResearchProcessor(IServiceScopeFactory scopes, IOptions<ResearchOptions> options, ILogger<ResearchProcessor> logger)
    : BackgroundService
{
    private static readonly TimeSpan ErrorBackoff = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var limits = options.Value;
        if (!limits.ProcessorEnabled)
        {
            logger.LogInformation("Research processor is disabled (Research:ProcessorEnabled=false)");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan wait;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var worked = await scope.ServiceProvider.GetRequiredService<IResearchRunner>().RunNextAsync(stoppingToken);
                wait = worked ? TimeSpan.Zero : limits.EffectivePoll;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Usually the database being unreachable. Back off instead of logging every poll.
                logger.LogWarning(ex, "Research processor could not poll for jobs; retrying in {Seconds}s", ErrorBackoff.TotalSeconds);
                wait = ErrorBackoff;
            }

            if (wait > TimeSpan.Zero)
            {
                try { await Task.Delay(wait, stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
