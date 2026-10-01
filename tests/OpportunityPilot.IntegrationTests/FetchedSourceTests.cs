using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpportunityPilot.Infrastructure.Research;

namespace OpportunityPilot.IntegrationTests;

/// <summary>
/// Url and Feed sources fetched over real sockets from a tiny HTTP server on loopback. Production never allows
/// loopback; these tests swap in a fetch policy that exists only in this test project (no configuration can
/// enable it) — or keep the strict address rules and prove the server is never contacted.
/// </summary>
public class FetchedSourceTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    /// <summary>Test-only: loopback and any port. The real policy is <see cref="StrictFetchAddressPolicy"/>.</summary>
    private sealed class LoopbackForTestsPolicy : IFetchAddressPolicy
    {
        public bool IsAllowed(IPAddress address) => IPAddress.IsLoopback(address) || AddressClassifier.IsPublic(address);
        public bool IsPortAllowed(int port) => true;
    }

    /// <summary>Production address rules, but any port, so the only thing standing between the fetcher and the server is the address check.</summary>
    private sealed class StrictAddressesAnyPortPolicy : IFetchAddressPolicy
    {
        public bool IsAllowed(IPAddress address) => AddressClassifier.IsPublic(address);
        public bool IsPortAllowed(int port) => true;
    }

    private sealed class TinyHttpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly Func<string, (string ContentType, string Body)> _respond;
        private int _connections;

        public TinyHttpServer(Func<string, (string ContentType, string Body)> respond)
        {
            _respond = respond;
            _listener.Start();
            _ = Task.Run(AcceptLoop);
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
        public int Connections => Volatile.Read(ref _connections);

        private async Task AcceptLoop()
        {
            while (!_stop.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await _listener.AcceptTcpClientAsync(_stop.Token); }
                catch (Exception) { return; }
                Interlocked.Increment(ref _connections);
                _ = Task.Run(async () =>
                {
                    using (client)
                    {
                        var stream = client.GetStream();
                        var buffer = new byte[8192];
                        var request = new StringBuilder();
                        while (!request.ToString().Contains("\r\n\r\n"))
                        {
                            var read = await stream.ReadAsync(buffer);
                            if (read == 0) return;
                            request.Append(Encoding.ASCII.GetString(buffer, 0, read));
                        }
                        var path = request.ToString().Split(' ')[1];
                        var (type, body) = _respond(path);
                        var bytes = Encoding.UTF8.GetBytes(body);
                        var head = $"HTTP/1.1 200 OK\r\nContent-Type: {type}; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n";
                        await stream.WriteAsync(Encoding.ASCII.GetBytes(head));
                        await stream.WriteAsync(bytes);
                    }
                });
            }
        }

        public ValueTask DisposeAsync()
        {
            _stop.Cancel();
            _listener.Stop();
            return ValueTask.CompletedTask;
        }
    }

    private const string Rss = """
        <?xml version="1.0"?>
        <rss version="2.0"><channel><title>Acme Careers</title>
          <item><title>Senior .NET Engineer</title><link>https://jobs.example/acme/1</link>
            <description>&lt;p&gt;Location: Pune&lt;/p&gt;&lt;p&gt;C#, .NET and Docker. 3-5 years.&lt;/p&gt;</description>
            <pubDate>Tue, 29 Sep 2026 10:00:00 GMT</pubDate></item>
          <item><title>Office Manager</title><link>https://jobs.example/acme/2</link>
            <description>Location: Pune. Run the office.</description></item>
        </channel></rss>
        """;

    private static readonly string LongPage =
        "<html><head><title>Platform Engineer - Acme</title><script>track()</script></head><body><nav>Menu</nav><main>" +
        "<h1>Platform Engineer</h1><p>Company: Acme</p><p>Location: Pune</p>" +
        string.Concat(Enumerable.Repeat("<p>We run C# and .NET services on Kubernetes with Docker; 3-5 years of experience. </p>", 4)) +
        "</main></body></html>";

    private WebApplicationFactory<Program> WithPolicy<TPolicy>() where TPolicy : class, IFetchAddressPolicy =>
        factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IFetchAddressPolicy, TPolicy>()));

    [Fact]
    public async Task A_feed_and_a_public_page_are_fetched_parsed_and_scored_and_a_script_only_page_needs_manual_input()
    {
        await using var server = new TinyHttpServer(path => path switch
        {
            "/feed.xml" => ("application/rss+xml", Rss),
            "/job" => ("text/html", LongPage),
            _ => ("text/html", "<html><body><div id=app></div><script>render()</script></body></html>")
        });
        await using var app = WithPolicy<LoopbackForTestsPolicy>();
        var user = PostgresApiFactory.ClientFor(app, "fetched-sources@example.test");
        var campaign = await ResearchApi.CreateCampaignAsync(user, "Job", new
        {
            requiredSkills = new[] { "C#", ".NET" }, candidateYears = 4, locations = new[] { "Pune" }
        });
        await ResearchApi.AddUrlAsync(user, campaign.Id(), "Feed", $"http://127.0.0.1:{server.Port}/feed.xml");
        await ResearchApi.AddUrlAsync(user, campaign.Id(), "Url", $"http://127.0.0.1:{server.Port}/job");
        await ResearchApi.AddUrlAsync(user, campaign.Id(), "Url", $"http://127.0.0.1:{server.Port}/spa");

        var jobId = await ResearchApi.QueueAsync(user, campaign.Id());
        await PostgresApiFactory.RunResearchAsync(app.Services);

        var job = await ResearchApi.JobAsync(user, jobId);
        Assert.Equal("Completed", job.Str("state"));            // a page needing manual input is not a failure
        Assert.Equal(3, job.GetProperty("counts").Int("candidates"));
        Assert.Contains(job.GetProperty("events").EnumerateArray(), e => e.Str("message").Contains("needs manual input"));

        var sources = (await user.GetJson($"/api/v1/campaigns/{campaign.Id()}/sources")).EnumerateArray().ToList();
        Assert.Equal(["Ok", "Ok", "Skipped"], sources.Select(s => s.Str("status")));
        Assert.Equal(2, sources[0].Int("itemCount"));
        Assert.StartsWith("NeedsManualInput", sources[2].Str("safeError"));

        var items = await ResearchApi.OpportunitiesAsync(user, campaign.Id());
        var fromFeed = items.Single(i => i.Str("title") == "Senior .NET Engineer");
        Assert.Equal("Qualified", fromFeed.Str("outcome"));
        Assert.Equal(100, fromFeed.Int("score"));
        Assert.Equal("Pune", fromFeed.Str("location"));
        Assert.Equal("https://jobs.example/acme/1", fromFeed.Str("url"));
        Assert.Equal("Excluded", items.Single(i => i.Str("title") == "Office Manager").Str("outcome"));

        var page = items.Single(i => i.Str("title") == "Platform Engineer - Acme");
        Assert.Equal("Acme", page.Str("organization"));
        var detail = await user.GetJson($"/api/v1/opportunities/{page.Id()}");
        var excerpt = detail.GetProperty("evidence")[0].Str("excerpt");
        Assert.Contains("Kubernetes", excerpt);
        Assert.DoesNotContain("track()", excerpt);
        Assert.DoesNotContain("Menu", excerpt);
    }

    [Fact]
    public async Task With_production_address_rules_a_loopback_server_is_never_contacted()
    {
        await using var server = new TinyHttpServer(_ => ("text/html", LongPage));
        await using var app = WithPolicy<StrictAddressesAnyPortPolicy>();
        var user = PostgresApiFactory.ClientFor(app, "fetched-blocked@example.test");
        var campaign = await ResearchApi.CreateCampaignAsync(user, "Job", new { requiredSkills = new[] { "C#" } });
        await ResearchApi.AddUrlAsync(user, campaign.Id(), "Url", $"http://127.0.0.1:{server.Port}/job");
        await ResearchApi.AddUrlAsync(user, campaign.Id(), "Feed", $"http://localhost:{server.Port}/feed.xml");

        var jobId = await ResearchApi.QueueAsync(user, campaign.Id());
        await PostgresApiFactory.RunResearchAsync(app.Services);

        Assert.Equal("CompletedWithGaps", (await ResearchApi.JobAsync(user, jobId)).Str("state"));
        Assert.Equal(0, server.Connections);
        Assert.All((await user.GetJson($"/api/v1/campaigns/{campaign.Id()}/sources")).EnumerateArray(),
            s => Assert.Equal("Failed", s.Str("status")));
    }
}
