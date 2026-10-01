using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpportunityPilot.Application.Research;
using OpportunityPilot.Infrastructure.Persistence;

namespace OpportunityPilot.IntegrationTests;

/// <summary>Leases, reclaiming and cancellation of durable jobs, against real PostgreSQL concurrency checks.</summary>
public class ResearchJobLifecycleTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static readonly object Criteria = new { requiredSkills = new[] { "C#" }, locations = new[] { "Pune" } };
    private const string Postings = "Dev One\nCompany: Acme\nLocation: Pune\nC#\n---\nDev Two\nCompany: Globex\nLocation: Pune\nC# and SQL";

    /// <summary>Simulates a processor that claimed the job and then died: Running, with a lease that has run out.</summary>
    private async Task AbandonAsync(Guid jobId, int minutesAgo = 10)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var job = await db.ResearchJobs.SingleAsync(j => j.Id == jobId);
        job.Claim(DateTime.UtcNow.AddMinutes(-minutesAgo), ResearchRunner.Lease);
        await db.SaveChangesAsync();
    }

    private async Task<(int State, int Attempts)> AttemptsAsync(Guid jobId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var job = await scope.ServiceProvider.GetRequiredService<AppDbContext>().ResearchJobs.AsNoTracking().SingleAsync(j => j.Id == jobId);
        return ((int)job.State, job.Attempts);
    }

    [Fact]
    public async Task Two_processors_racing_for_one_job_run_it_once()
    {
        var user = factory.ClientFor("lifecycle-race@example.test");
        var campaign = await ResearchApi.CreateCampaignAsync(user, "Job", Criteria);
        await ResearchApi.AddPasteAsync(user, campaign.Id(), Postings);
        var jobId = await ResearchApi.QueueAsync(user, campaign.Id());

        async Task<bool> Processor()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IResearchRunner>().RunNextAsync(CancellationToken.None);
        }
        var results = await Task.WhenAll(Processor(), Processor());

        Assert.Equal(1, results.Count(r => r));
        Assert.Equal("Completed", (await ResearchApi.JobAsync(user, jobId)).Str("state"));
        Assert.Equal(1, (await AttemptsAsync(jobId)).Attempts);
        Assert.Equal(2, (await ResearchApi.OpportunitiesAsync(user, campaign.Id())).Count);
    }

    [Fact]
    public async Task An_expired_lease_is_reclaimed_and_the_rerun_does_not_duplicate_opportunities()
    {
        var user = factory.ClientFor("lifecycle-reclaim@example.test");
        var campaign = await ResearchApi.CreateCampaignAsync(user, "Job", Criteria);
        await ResearchApi.AddPasteAsync(user, campaign.Id(), Postings);
        await ResearchApi.QueueAsync(user, campaign.Id());
        await factory.RunResearchAsync();
        var before = (await ResearchApi.OpportunitiesAsync(user, campaign.Id())).Select(o => o.Id()).Order().ToList();

        var jobId = await ResearchApi.QueueAsync(user, campaign.Id());
        await AbandonAsync(jobId);
        Assert.Equal("Running", (await ResearchApi.JobAsync(user, jobId)).Str("state"));

        Assert.True(await factory.RunResearchAsync() >= 1);

        Assert.Equal("Completed", (await ResearchApi.JobAsync(user, jobId)).Str("state"));
        Assert.Equal(2, (await AttemptsAsync(jobId)).Attempts);
        Assert.Equal(before, (await ResearchApi.OpportunitiesAsync(user, campaign.Id())).Select(o => o.Id()).Order().ToList());
    }

    [Fact]
    public async Task A_running_job_that_is_cancelled_stops_between_sources_and_ends_cancelled()
    {
        var user = factory.ClientFor("lifecycle-cancel@example.test");
        var campaign = await ResearchApi.CreateCampaignAsync(user, "Job", Criteria);
        await ResearchApi.AddPasteAsync(user, campaign.Id(), Postings);
        var jobId = await ResearchApi.QueueAsync(user, campaign.Id());
        await AbandonAsync(jobId);

        var flagged = await (await user.PostAsync($"/api/v1/research-jobs/{jobId}/cancel", null)).Json(System.Net.HttpStatusCode.Accepted);
        Assert.Equal("Running", flagged.Str("state"));   // a running job is only flagged; the processor stops it

        await factory.RunResearchAsync();

        var job = await ResearchApi.JobAsync(user, jobId);
        Assert.Equal("Cancelled", job.Str("state"));
        Assert.Contains(job.GetProperty("events").EnumerateArray(), e => e.Str("message").StartsWith("Cancelled"));
        Assert.Empty(await ResearchApi.OpportunitiesAsync(user, campaign.Id()));
    }

    [Fact]
    public async Task Run_limits_cap_candidates_and_the_result_limit_caps_new_opportunities_best_first()
    {
        await using var limited = factory.WithWebHostBuilder(b => b.UseSetting("Research:MaxCandidates", "3"));
        var user = PostgresApiFactory.ClientFor(limited, "lifecycle-limits@example.test");
        var campaign = await ResearchApi.CreateCampaignAsync(user, "Job", Criteria, resultLimit: 1);
        var preview = await (await user.PostAsJsonAsync("/api/v1/imports/preview", new
        {
            campaignId = campaign.Id(),
            csv = "title,company,location,description\nA,Acme,Mumbai,C#\nB,Globex,Pune,C#\nC,Initech,Pune,Java\nD,Hooli,Pune,C#\n"
        })).Json(System.Net.HttpStatusCode.OK);
        await (await user.PostAsJsonAsync($"/api/v1/imports/{preview.GetProperty("importId").GetGuid()}/commit", new { }))
            .Json(System.Net.HttpStatusCode.Created);

        var jobId = await ResearchApi.QueueAsync(user, campaign.Id());
        await PostgresApiFactory.RunResearchAsync(limited.Services);

        var job = await ResearchApi.JobAsync(user, jobId);
        Assert.Equal(3, job.GetProperty("counts").Int("candidates"));
        Assert.Equal(4, job.GetProperty("counts").Int("fetched"));
        Assert.Contains(job.GetProperty("events").EnumerateArray(), e => e.Str("message").Contains("1 items beyond the run's limit of 3"));
        Assert.Contains(job.GetProperty("events").EnumerateArray(), e => e.Str("message").Contains("beyond the campaign's result limit (1)"));
        Assert.Equal(4, (await user.GetJson($"/api/v1/campaigns/{campaign.Id()}/sources"))[0].Int("itemCount"));

        var saved = Assert.Single(await ResearchApi.OpportunitiesAsync(user, campaign.Id()));
        Assert.Equal("Qualified", saved.Str("outcome"));     // a qualified candidate wins the single slot
    }

    [Fact]
    public async Task A_job_interrupted_three_times_is_failed_instead_of_retried_forever()
    {
        var user = factory.ClientFor("lifecycle-poison@example.test");
        var campaign = await ResearchApi.CreateCampaignAsync(user, "Job", Criteria);
        await ResearchApi.AddPasteAsync(user, campaign.Id(), Postings);
        var jobId = await ResearchApi.QueueAsync(user, campaign.Id());
        foreach (var minutesAgo in new[] { 30, 20, 10 }) await AbandonAsync(jobId, minutesAgo);

        await factory.RunResearchAsync();

        var job = await ResearchApi.JobAsync(user, jobId);
        Assert.Equal("Failed", job.Str("state"));
        Assert.Contains("3 interrupted attempts", job.Str("safeError"));
    }
}
