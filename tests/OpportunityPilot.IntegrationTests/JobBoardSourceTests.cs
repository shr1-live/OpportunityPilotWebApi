using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpportunityPilot.Infrastructure.Research;

namespace OpportunityPilot.IntegrationTests;

/// <summary>
/// Greenhouse, Lever and Adzuna sources researched end to end through the real SafeFetcher, over real sockets, against a
/// loopback stand-in for the public APIs. The API roots are redirected with Research:*ApiBase; loopback is reachable only
/// because these tests register the test-only <see cref="LoopbackForTestsPolicy"/> (no configuration can).
/// </summary>
public class JobBoardSourceTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private const string FakeAdzunaKey = "fake-adzuna-key-for-tests-7f3a";

    private static readonly object Criteria = new
    {
        keywords = new[] { ".NET" },
        requiredSkills = new[] { "C#", ".NET" },
        locations = new[] { "Pune" }
    };

    private const string GreenhouseList = """
        {
          "jobs": [
            { "id": 101, "title": "Senior .NET Engineer", "absolute_url": "https://boards.example/acme/jobs/101",
              "location": { "name": "Pune, India" }, "updated_at": "2026-10-01T10:00:00-04:00", "company_name": "Acme Corp",
              "first_published": "2026-09-20T10:00:00-04:00" },
            { "id": 102, "title": ".NET Platform Developer", "absolute_url": "https://boards.example/acme/jobs/102",
              "location": { "name": "Pune" }, "updated_at": "2026-09-30T10:00:00-04:00", "company_name": "Acme Corp" },
            { "id": 103, "title": "Office Manager", "absolute_url": "https://boards.example/acme/jobs/103",
              "location": { "name": "Pune" }, "updated_at": "2026-10-02T10:00:00-04:00", "company_name": "Acme Corp" }
          ],
          "meta": { "total": 3 }
        }
        """;

    private const string GreenhouseDetail101 = """
        { "id": 101, "title": "Senior .NET Engineer",
          "content": "&lt;h2&gt;About&lt;/h2&gt;&lt;p&gt;We build &lt;strong&gt;C# and .NET&lt;/strong&gt; services on Docker. 3-5 years.&lt;/p&gt;" }
        """;

    private const string GreenhouseDetail102 = """
        { "id": 102, "title": ".NET Platform Developer", "content": "&lt;p&gt;C# and .NET platform work.&lt;/p&gt;" }
        """;

    private const string LeverPostings = """
        [
          { "id": "aaaa-1111", "text": "Backend Engineer (.NET)", "hostedUrl": "https://jobs.example/leverdemo/aaaa-1111",
            "applyUrl": "https://jobs.example/leverdemo/aaaa-1111/apply",
            "categories": { "location": "Pune", "team": "Engineering", "allLocations": ["Pune"] }, "workplaceType": "hybrid",
            "descriptionPlain": "Build C# services.", "lists": [{ "text": "Requirements", "content": "<li>.NET 8</li>" }],
            "createdAt": 1727700000000, "country": "IN" },
          { "id": "bbbb-2222", "text": "Recruiter", "hostedUrl": "https://jobs.example/leverdemo/bbbb-2222",
            "applyUrl": "https://jobs.example/leverdemo/bbbb-2222/apply",
            "categories": { "location": "Pune" }, "workplaceType": "on-site", "descriptionPlain": "Hire people.", "lists": [] }
        ]
        """;

    private const string AdzunaResults = """
        { "count": 1, "results": [
          { "id": "4567890123", "title": "Senior <strong>.NET</strong> Developer", "company": { "display_name": "Globex" },
            "location": { "display_name": "Pune, Maharashtra" }, "redirect_url": "https://adzuna.example/land/ad/4567890123",
            "description": "C# and .NET APIs.", "created": "2026-09-29T08:00:00Z" } ] }
        """;

    private static (int, string, string) Ok(string json) => (200, "application/json", json);

    private static (int, string, string) Board(string path) => path switch
    {
        "/v1/boards/acme/jobs" => Ok(GreenhouseList),
        "/v1/boards/acme/jobs/101" => Ok(GreenhouseDetail101),
        "/v1/boards/acme/jobs/102" => Ok(GreenhouseDetail102),
        "/v0/postings/leverdemo?mode=json&limit=100" => Ok(LeverPostings),
        _ when path.StartsWith("/v1/api/jobs/in/search/1?", StringComparison.Ordinal) => Ok(AdzunaResults),
        _ => (404, "application/json", "{\"status\":404,\"error\":\"Not found\"}")
    };

    private WebApplicationFactory<Program> App(TinyHttpServer server, params (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(b =>
        {
            b.ConfigureTestServices(s => s.AddSingleton<IFetchAddressPolicy, LoopbackForTestsPolicy>());
            b.UseSetting("Research:GreenhouseApiBase", server.BaseUrl);
            b.UseSetting("Research:LeverApiBase", server.BaseUrl);
            b.UseSetting("Research:AdzunaApiBase", server.BaseUrl);
            b.UseSetting("Adzuna:AppId", "");
            b.UseSetting("Adzuna:AppKey", "");
            foreach (var (key, value) in settings) b.UseSetting(key, value);
        });

    private static List<string> Events(JsonElement job) =>
        job.GetProperty("events").EnumerateArray().Select(e => e.Str("message")).ToList();

    [Fact]
    public async Task Greenhouse_and_lever_sources_are_fetched_mapped_scored_and_deduped_on_rerun()
    {
        await using var server = new TinyHttpServer(Board);
        await using var app = App(server);
        var user = PostgresApiFactory.ClientFor(app, "boards-flow@example.test");
        var campaign = await ResearchApi.CreateCampaignAsync(user, "Job", Criteria);

        var greenhouse = await ResearchApi.AddBoardAsync(user, campaign.Id(), "Greenhouse", "https://boards.greenhouse.io/acme");
        var lever = await ResearchApi.AddBoardAsync(user, campaign.Id(), "Lever", "jobs.lever.co/leverdemo");
        Assert.Equal(("acme", "Greenhouse board acme", "Pending"), (greenhouse.Str("url"), greenhouse.Str("label"), greenhouse.Str("status")));
        Assert.Equal(("leverdemo", "Lever company leverdemo"), (lever.Str("url"), lever.Str("label")));
        Assert.Equal(0, server.Connections);                                      // adding a source never calls the API

        var jobId = await ResearchApi.QueueAsync(user, campaign.Id());
        await PostgresApiFactory.RunResearchAsync(app.Services);

        var job = await ResearchApi.JobAsync(user, jobId);
        Assert.Equal("Completed", job.Str("state"));
        Assert.Equal(4, job.GetProperty("counts").Int("candidates"));
        var events = Events(job);
        Assert.Contains("Greenhouse board acme: 3 jobs listed, 2 matched your keywords, 2 read.", events);
        Assert.Contains("Lever company leverdemo: 2 postings listed, 2 read.", events);

        // Titles are pre-filtered before descriptions are fetched: the Office Manager description is never requested.
        Assert.Contains("/v1/boards/acme/jobs/101", server.Paths);
        Assert.Contains("/v1/boards/acme/jobs/102", server.Paths);
        Assert.DoesNotContain("/v1/boards/acme/jobs/103", server.Paths);

        var sources = (await user.GetJson($"/api/v1/campaigns/{campaign.Id()}/sources")).EnumerateArray().ToList();
        Assert.All(sources, s => Assert.Equal("Ok", s.Str("status")));
        Assert.Equal([2, 2], sources.Select(s => s.Int("itemCount")));

        var items = await ResearchApi.OpportunitiesAsync(user, campaign.Id());
        Assert.Equal(4, items.Count);
        var senior = items.Single(i => i.Str("title") == "Senior .NET Engineer");
        Assert.Equal("Greenhouse", senior.Str("platform"));
        Assert.Equal("Acme Corp", senior.Str("organization"));
        Assert.Equal("Pune, India", senior.Str("location"));
        Assert.Equal("https://boards.example/acme/jobs/101", senior.Str("url"));
        Assert.Equal("https://boards.example/acme/jobs/101", senior.Str("applyUrl"));
        Assert.Equal("Qualified", senior.Str("outcome"));
        var detail = await user.GetJson($"/api/v1/opportunities/{senior.Id()}");
        Assert.Contains("We build C# and .NET services on Docker.", detail.Str("description"));
        Assert.DoesNotContain("<", detail.Str("description"));
        Assert.Equal("Greenhouse board acme", detail.GetProperty("evidence")[0].Str("sourceLabel"));

        var backend = items.Single(i => i.Str("title") == "Backend Engineer (.NET)");
        Assert.Equal("Lever", backend.Str("platform"));
        Assert.Equal("Leverdemo", backend.Str("organization"));
        Assert.Equal("https://jobs.example/leverdemo/aaaa-1111", backend.Str("url"));
        Assert.Equal("https://jobs.example/leverdemo/aaaa-1111/apply", backend.Str("applyUrl"));
        Assert.Equal("Qualified", backend.Str("outcome"));
        Assert.Equal("Excluded", items.Single(i => i.Str("title") == "Recruiter").Str("outcome"));
        // No auto-suggest threshold on this campaign: research leaves every status New.
        Assert.All(items, i => Assert.Equal("New", i.Str("status")));

        // Rerun: dedupe by job:{platform}:{externalId} updates instead of adding.
        await ResearchApi.QueueAsync(user, campaign.Id());
        await PostgresApiFactory.RunResearchAsync(app.Services);
        Assert.Equal(4, (await ResearchApi.OpportunitiesAsync(user, campaign.Id())).Count);
    }

    [Fact]
    public async Task The_runs_fetch_limit_bounds_a_board_and_skips_the_sources_after_it()
    {
        await using var server = new TinyHttpServer(Board);
        await using var app = App(server, ("Research:MaxFetches", "2"));
        var user = PostgresApiFactory.ClientFor(app, "boards-budget@example.test");
        var campaign = await ResearchApi.CreateCampaignAsync(user, "Job", Criteria);
        await ResearchApi.AddBoardAsync(user, campaign.Id(), "Greenhouse", "acme");
        await ResearchApi.AddBoardAsync(user, campaign.Id(), "Lever", "leverdemo");

        var jobId = await ResearchApi.QueueAsync(user, campaign.Id());
        await PostgresApiFactory.RunResearchAsync(app.Services);

        var job = await ResearchApi.JobAsync(user, jobId);
        Assert.Equal("Completed", job.Str("state"));
        Assert.Contains("Greenhouse board acme: 3 jobs listed, 2 matched your keywords, 1 read. Stopped at the run's limit of 2 fetches.", Events(job));
        Assert.Equal(2, server.Paths.Count);
        var sources = (await user.GetJson($"/api/v1/campaigns/{campaign.Id()}/sources")).EnumerateArray().ToList();
        Assert.Equal(["Ok", "Skipped"], sources.Select(s => s.Str("status")));
        Assert.StartsWith("Skipped: this run already used its 2 page fetches.", sources[1].Str("safeError"));
    }

    [Fact]
    public async Task Adzuna_without_keys_and_an_unknown_board_fail_safely_and_the_run_completes_with_gaps()
    {
        await using var server = new TinyHttpServer(Board);
        await using var app = App(server);
        var user = PostgresApiFactory.ClientFor(app, "boards-gaps@example.test");
        var campaign = await ResearchApi.CreateCampaignAsync(user, "Job", Criteria);
        await ResearchApi.AddPasteAsync(user, campaign.Id(), "Senior .NET Developer\nCompany: Initech\nLocation: Pune\nC# and .NET");
        var adzuna = await ResearchApi.AddBoardAsync(user, campaign.Id(), "Adzuna");
        await ResearchApi.AddBoardAsync(user, campaign.Id(), "Greenhouse", "nobody");
        Assert.Equal("Adzuna search", adzuna.Str("label"));
        Assert.Equal(JsonValueKind.Null, adzuna.GetProperty("url").ValueKind);

        var jobId = await ResearchApi.QueueAsync(user, campaign.Id());
        await PostgresApiFactory.RunResearchAsync(app.Services);

        var job = await ResearchApi.JobAsync(user, jobId);
        Assert.Equal("CompletedWithGaps", job.Str("state"));
        Assert.Equal(2, job.GetProperty("counts").Int("sourcesFailed"));
        var sources = (await user.GetJson($"/api/v1/campaigns/{campaign.Id()}/sources")).EnumerateArray().ToList();
        Assert.Equal(["Ok", "Failed", "Failed"], sources.Select(s => s.Str("status")));
        Assert.Equal("Adzuna is not configured on the server.", sources[1].Str("safeError"));
        Assert.Equal("No public Greenhouse board was found for \"nobody\". Check the board token.", sources[2].Str("safeError"));
        Assert.DoesNotContain(server.Paths, p => p.StartsWith("/v1/api/", StringComparison.Ordinal));   // no keys → no request
        Assert.Single(await ResearchApi.OpportunitiesAsync(user, campaign.Id()));

        var adzunaCapability = (await user.GetJson("/api/v1/capabilities")).GetProperty("items").EnumerateArray().Single(i => i.Str("key") == "adzuna");
        Assert.Equal("NotConfigured", adzunaCapability.Str("status"));
    }

    [Fact]
    public async Task Adzuna_with_server_keys_searches_and_never_records_the_key()
    {
        await using var server = new TinyHttpServer(Board);
        await using var app = App(server, ("Adzuna:AppId", "fake-app-id"), ("Adzuna:AppKey", FakeAdzunaKey));
        var user = PostgresApiFactory.ClientFor(app, "boards-adzuna@example.test");
        var campaign = await ResearchApi.CreateCampaignAsync(user, "Job", new
        {
            keywords = new[] { ".NET developer", "C#" }, requiredSkills = new[] { "C#" }, locations = new[] { "Remote", "Pune" }
        });
        await ResearchApi.AddBoardAsync(user, campaign.Id(), "Adzuna");

        var jobId = await ResearchApi.QueueAsync(user, campaign.Id());
        await PostgresApiFactory.RunResearchAsync(app.Services);

        var job = await ResearchApi.JobAsync(user, jobId);
        Assert.Equal("Completed", job.Str("state"));
        Assert.Contains("Adzuna search: searched 2 keywords in Pune, 1 job found, 1 read.", Events(job));
        var requests = server.Paths.Where(p => p.StartsWith("/v1/api/jobs/in/search/1?", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, requests.Count);
        Assert.All(requests, p => Assert.Contains($"app_key={FakeAdzunaKey}", p));
        Assert.Contains("&where=Pune&", requests[0]);

        var item = Assert.Single(await ResearchApi.OpportunitiesAsync(user, campaign.Id()));
        Assert.Equal(("Senior .NET Developer", "Globex", "Adzuna"), (item.Str("title"), item.Str("organization"), item.Str("platform")));
        Assert.Equal("https://adzuna.example/land/ad/4567890123", item.Str("applyUrl"));

        var everything = string.Join("\n",
            JsonSerializer.Serialize(job),
            JsonSerializer.Serialize(await user.GetJson($"/api/v1/campaigns/{campaign.Id()}/sources")),
            JsonSerializer.Serialize(await user.GetJson($"/api/v1/opportunities/{item.Id()}")),
            await user.GetStringAsync("/api/v1/capabilities"));
        Assert.DoesNotContain(FakeAdzunaKey, everything);
        Assert.DoesNotContain("fake-app-id", everything);
        Assert.DoesNotContain("app_key", everything);
    }

    [Fact]
    public async Task Board_sources_validate_their_identifier_and_are_for_job_campaigns_only()
    {
        var user = factory.ClientFor("boards-validation@example.test");
        var job = await ResearchApi.CreateCampaignAsync(user, "Job", Criteria);
        var customer = await ResearchApi.CreateCampaignAsync(user, "Customer", new { industries = new[] { "fintech" } });

        async Task<string> Rejected(Guid campaignId, object body)
        {
            var response = await user.PostAsJsonAsync($"/api/v1/campaigns/{campaignId}/sources", body);
            var text = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest, text);
            return string.Join(",", JsonDocument.Parse(text).RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name));
        }

        Assert.Equal("url", await Rejected(job.Id(), new { kind = "Greenhouse" }));
        Assert.Equal("url", await Rejected(job.Id(), new { kind = "Greenhouse", url = "https://evil.example/acme" }));
        Assert.Equal("url", await Rejected(job.Id(), new { kind = "Greenhouse", url = "acme corp" }));
        Assert.Equal("url", await Rejected(job.Id(), new { kind = "Lever", url = "https://boards.greenhouse.io/acme" }));
        Assert.Equal("url", await Rejected(job.Id(), new { kind = "Lever", url = new string('a', 101) }));
        Assert.Equal("kind", await Rejected(customer.Id(), new { kind = "Greenhouse", url = "acme" }));
        Assert.Equal("kind", await Rejected(customer.Id(), new { kind = "Lever", url = "leverdemo" }));
        Assert.Equal("kind", await Rejected(customer.Id(), new { kind = "Adzuna" }));

        var added = await ResearchApi.AddBoardAsync(user, job.Id(), "Greenhouse", "https://job-boards.greenhouse.io/Stripe/jobs/7171717");
        Assert.Equal(("Greenhouse", "stripe"), (added.Str("kind"), added.Str("url")));
        // One board per source: the same kind can be added again for another company.
        await ResearchApi.AddBoardAsync(user, job.Id(), "Greenhouse", "acme");
        Assert.Equal(2, (await user.GetJson($"/api/v1/campaigns/{job.Id()}/sources")).GetArrayLength());

        var bob = factory.ClientFor("boards-validation-bob@example.test");
        Assert.Equal(HttpStatusCode.NotFound,
            (await bob.PostAsJsonAsync($"/api/v1/campaigns/{job.Id()}/sources", new { kind = "Greenhouse", url = "acme" })).StatusCode);
    }

    [Fact]
    public async Task With_production_address_rules_a_redirected_api_base_on_loopback_is_never_contacted()
    {
        await using var server = new TinyHttpServer(Board);
        await using var app = factory.WithWebHostBuilder(b =>
        {
            b.ConfigureTestServices(s => s.AddSingleton<IFetchAddressPolicy, StrictAddressesAnyPortPolicy>());
            b.UseSetting("Research:GreenhouseApiBase", server.BaseUrl);
            b.UseSetting("Research:LeverApiBase", $"http://localhost:{server.Port}");
        });
        var user = PostgresApiFactory.ClientFor(app, "boards-blocked@example.test");
        var campaign = await ResearchApi.CreateCampaignAsync(user, "Job", Criteria);
        await ResearchApi.AddBoardAsync(user, campaign.Id(), "Greenhouse", "acme");
        await ResearchApi.AddBoardAsync(user, campaign.Id(), "Lever", "leverdemo");

        var jobId = await ResearchApi.QueueAsync(user, campaign.Id());
        await PostgresApiFactory.RunResearchAsync(app.Services);

        Assert.Equal("CompletedWithGaps", (await ResearchApi.JobAsync(user, jobId)).Str("state"));
        Assert.Equal(0, server.Connections);
        Assert.All((await user.GetJson($"/api/v1/campaigns/{campaign.Id()}/sources")).EnumerateArray(),
            s => Assert.Equal("The address is private, local or reserved, so it is never fetched.", s.Str("safeError")));
    }
}
