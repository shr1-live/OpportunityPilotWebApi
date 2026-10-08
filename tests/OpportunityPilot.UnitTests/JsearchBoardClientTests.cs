using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpportunityPilot.Application.Configuration;
using OpportunityPilot.Application.JobBoards;
using OpportunityPilot.Infrastructure.Research;

namespace OpportunityPilot.UnitTests;

public class JsearchBoardClientTests
{
    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    private static (JsearchBoardClient Client, Handler Handler) Create(string? key, HttpStatusCode status = HttpStatusCode.OK, string body = """{"data":[]}""")
    {
        var handler = new Handler(status, body);
        var client = new JsearchBoardClient(new HttpClient(handler), Options.Create(new JsearchOptions { Key = key }),
            TimeProvider.System, NullLogger<JsearchBoardClient>.Instance);
        return (client, handler);
    }

    private static JobBoardSearchRequest Request(JobBoard board, string query = "unique " ) =>
        new(board, query + Guid.NewGuid().ToString("N"), null, false, "week", null, 1);

    [Fact]
    public async Task Without_a_key_nothing_is_called()
    {
        var (client, handler) = Create(null);
        var result = await client.SearchAsync(Request(JobBoard.Indeed), default);
        Assert.Equal("NotConfigured", result.Status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Sends_the_key_as_a_header_only_and_defaults_seek_to_australia()
    {
        var (client, handler) = Create("secret-key");
        await client.SearchAsync(Request(JobBoard.Seek), default);
        var sent = Assert.Single(handler.Requests);
        Assert.Equal("secret-key", sent.Headers.GetValues("X-RapidAPI-Key").Single());
        Assert.DoesNotContain("secret-key", sent.RequestUri!.ToString());
        Assert.Contains("country=au", sent.RequestUri!.Query);
        Assert.Contains("via%20SEEK", sent.RequestUri!.Query);
    }

    [Fact]
    public async Task Repeated_search_is_served_from_cache()
    {
        var (client, handler) = Create("k");
        var request = Request(JobBoard.LinkedIn);
        await client.SearchAsync(request, default);
        var second = await client.SearchAsync(request, default);
        Assert.True(second.FromCache);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "refused")]
    [InlineData(HttpStatusCode.TooManyRequests, "quota")]
    public async Task Provider_errors_are_reported_plainly(HttpStatusCode status, string word)
    {
        var (client, _) = Create("k", status, "{}");
        var result = await client.SearchAsync(Request(JobBoard.Indeed), default);
        Assert.Equal("Failed", result.Status);
        Assert.Contains(word, result.Message);
    }
}
