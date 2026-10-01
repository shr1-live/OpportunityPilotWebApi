using System.Net;
using System.Text;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpportunityPilot.Application.Research;
using OpportunityPilot.Infrastructure.Research;

namespace OpportunityPilot.UnitTests.Research;

public class SafeFetcherTests
{
    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    /// <summary>Answers from a script and records every request that actually reached "the network".</summary>
    private sealed class ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(respond(request));
        }
    }

    private static SafeFetcher Fetcher(HttpMessageHandler handler, string environment = "Production", ResearchOptions? options = null)
    {
        var o = Options.Create(options ?? new ResearchOptions());
        return new SafeFetcher(new HttpClient(handler), new StrictFetchAddressPolicy(), new Env(environment), o,
            new FetchConcurrency(o), NullLogger<SafeFetcher>.Instance);
    }

    private static HttpResponseMessage Html(string body, string type = "text/html") =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, type) };

    private static HttpResponseMessage Redirect(string location)
    {
        var r = new HttpResponseMessage(HttpStatusCode.Found);
        r.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
        return r;
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.10.20.30")]
    [InlineData("10.0.0.5")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]       // cloud metadata
    [InlineData("169.254.0.1")]
    [InlineData("100.64.0.1")]            // CGNAT
    [InlineData("100.127.255.254")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]             // multicast
    [InlineData("239.255.255.250")]
    [InlineData("240.0.0.1")]             // reserved
    [InlineData("255.255.255.255")]
    [InlineData("192.0.2.10")]            // documentation
    [InlineData("198.18.0.1")]            // benchmarking
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fe80::1")]               // link-local
    [InlineData("fc00::1")]               // unique local
    [InlineData("fd12:3456::1")]
    [InlineData("ff02::1")]               // multicast
    [InlineData("::ffff:127.0.0.1")]      // IPv4-mapped loopback
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("::ffff:10.1.2.3")]
    [InlineData("::127.0.0.1")]           // IPv4-compatible (deprecated)
    [InlineData("64:ff9b::a00:1")]        // NAT64 of 10.0.0.1
    [InlineData("2002:7f00:1::1")]        // 6to4 of 127.0.0.1
    [InlineData("2001:db8::1")]           // documentation
    [InlineData("2001:0:4136:e378::1")]   // Teredo
    public void Private_local_and_reserved_addresses_are_blocked(string ip) =>
        Assert.False(AddressClassifier.IsPublic(IPAddress.Parse(ip)));

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("93.184.215.14")]
    [InlineData("172.32.0.1")]            // just outside 172.16/12
    [InlineData("100.128.0.1")]           // just outside 100.64/10
    [InlineData("2606:4700:4700::1111")]
    [InlineData("::ffff:8.8.8.8")]
    [InlineData("2002:808:808::1")]       // 6to4 of 8.8.8.8
    public void Public_addresses_are_allowed(string ip) =>
        Assert.True(AddressClassifier.IsPublic(IPAddress.Parse(ip)));

    [Theory]
    [InlineData("http://example.com/", "Production", "https")]
    [InlineData("https://user:pw@example.com/", "Production", "username")]
    [InlineData("https://example.com:8443/", "Production", "ports")]
    [InlineData("ftp://example.com/", "Production", "https")]
    [InlineData("file:///etc/passwd", "Production", "https")]
    [InlineData("not a url", "Production", "absolute")]
    public void Url_shape_is_checked_before_anything_is_fetched(string url, string env, string reasonContains)
    {
        var handler = new ScriptedHandler(_ => Html("never"));
        var reason = Fetcher(handler, env).CheckUrl(url);
        Assert.NotNull(reason);
        Assert.Contains(reasonContains, reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Http_is_allowed_only_in_development()
    {
        var handler = new ScriptedHandler(_ => Html("x"));
        Assert.NotNull(Fetcher(handler, "Production").CheckUrl("http://example.com/"));
        Assert.Null(Fetcher(handler, "Development").CheckUrl("http://example.com/"));
        Assert.Null(Fetcher(handler, "Production").CheckUrl("https://example.com/jobs"));
    }

    [Theory]
    [InlineData("https://127.0.0.1/")]
    [InlineData("https://169.254.169.254/latest/meta-data/")]
    [InlineData("https://[::1]/")]
    [InlineData("https://[::ffff:127.0.0.1]/")]
    [InlineData("https://localhost/")]
    [InlineData("https://api.localhost/")]
    public async Task Blocked_destinations_fail_safely_without_any_request(string url)
    {
        var handler = new ScriptedHandler(_ => Html("should not be reached"));

        var result = await Fetcher(handler).FetchAsync(url, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Contains("private, local or reserved", result.FailureReason);
        Assert.Empty(handler.Requests);
        Assert.Equal(0, result.Requests);
    }

    [Fact]
    public async Task A_redirect_to_the_metadata_address_is_refused_at_that_hop()
    {
        var handler = new ScriptedHandler(r => r.RequestUri!.Host == "example.com"
            ? Redirect("https://169.254.169.254/latest/meta-data/")
            : Html("secret"));

        var result = await Fetcher(handler).FetchAsync("https://example.com/start", CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Contains("private, local or reserved", result.FailureReason);
        Assert.Equal(["https://example.com/start"], handler.Requests.Select(u => u.ToString()));
    }

    [Fact]
    public async Task Redirects_are_followed_by_hand_up_to_five_times()
    {
        var handler = new ScriptedHandler(r =>
        {
            var n = int.Parse(r.RequestUri!.AbsolutePath.Trim('/'));
            return n < 5 ? Redirect($"/{n + 1}") : Html("<p>done</p>");
        });
        var ok = await Fetcher(handler).FetchAsync("https://example.com/0", CancellationToken.None);
        Assert.True(ok.Ok);
        Assert.Equal("https://example.com/5", ok.FinalUrl);
        Assert.Equal(6, ok.Requests);

        var endless = new ScriptedHandler(r => Redirect($"/{int.Parse(r.RequestUri!.AbsolutePath.Trim('/')) + 1}"));
        var tooMany = await Fetcher(endless).FetchAsync("https://example.com/0", CancellationToken.None);
        Assert.False(tooMany.Ok);
        Assert.Contains("redirected more than 5", tooMany.FailureReason);
        Assert.Equal(6, endless.Requests.Count);
    }

    [Fact]
    public async Task A_redirect_to_plain_http_is_refused_outside_development()
    {
        var handler = new ScriptedHandler(_ => Redirect("http://example.com/insecure"));
        var result = await Fetcher(handler).FetchAsync("https://example.com/", CancellationToken.None);
        Assert.False(result.Ok);
        Assert.Contains("https", result.FailureReason);
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("application/octet-stream")]
    [InlineData("application/pdf")]
    public async Task Binary_content_types_are_rejected(string type)
    {
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([1, 2, 3]) { Headers = { ContentType = new(type) } }
        });
        var result = await Fetcher(handler).FetchAsync("https://example.com/file", CancellationToken.None);
        Assert.False(result.Ok);
        Assert.Contains("not read", result.FailureReason);
    }

    [Fact]
    public async Task Bodies_over_the_cap_are_rejected_even_without_a_content_length()
    {
        var big = new string('a', 4096);
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(big))) { Headers = { ContentType = new("text/html") } }
        });
        var result = await Fetcher(handler, options: new ResearchOptions { MaxBytes = 1024 }).FetchAsync("https://example.com/", CancellationToken.None);
        Assert.False(result.Ok);
        Assert.Contains("larger than 1 KB", result.FailureReason);
    }

    [Fact]
    public async Task Server_errors_are_retried_three_times_then_fail_safely()
    {
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var result = await Fetcher(handler).FetchAsync("https://example.com/", CancellationToken.None);
        Assert.False(result.Ok);
        Assert.Equal("The site answered 503.", result.FailureReason);
        Assert.Equal(4, handler.Requests.Count);
    }

    [Fact]
    public async Task A_login_wall_is_reported_plainly_and_not_retried()
    {
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var result = await Fetcher(handler).FetchAsync("https://example.com/", CancellationToken.None);
        Assert.Contains("may need a login", result.FailureReason);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task A_public_html_page_is_returned_as_text()
    {
        var handler = new ScriptedHandler(_ => Html("<html><title>Hi</title><body>Hello</body></html>"));
        var result = await Fetcher(handler).FetchAsync("https://example.com/", CancellationToken.None);
        Assert.True(result.Ok);
        Assert.Equal("text/html", result.ContentType);
        Assert.Contains("Hello", result.Content);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.7")]
    [InlineData("169.254.169.254")]
    public async Task Dns_answers_are_checked_in_the_connect_callback(string answer)
    {
        // The real production handler, with DNS replaced: the name looks harmless, the answer is private (rebinding).
        var resolved = new List<string>();
        var handler = SafeFetcher.CreateHandler(new StrictFetchAddressPolicy(), (host, _) =>
        {
            resolved.Add(host);
            return Task.FromResult(new[] { IPAddress.Parse(answer) });
        });

        var result = await Fetcher(handler).FetchAsync("https://rebinding.example/", CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Contains("private, local or reserved", result.FailureReason);
        Assert.Equal(["rebinding.example"], resolved);
    }

    [Fact]
    public async Task A_mixed_dns_answer_with_any_private_address_is_refused()
    {
        var handler = SafeFetcher.CreateHandler(new StrictFetchAddressPolicy(),
            (_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8"), IPAddress.Parse("192.168.0.10") }));
        var result = await Fetcher(handler).FetchAsync("https://mixed.example/", CancellationToken.None);
        Assert.False(result.Ok);
        Assert.Contains("private, local or reserved", result.FailureReason);
    }

    [Fact]
    public void The_production_handler_never_follows_redirects_or_uses_a_proxy()
    {
        using var handler = SafeFetcher.CreateHandler(new StrictFetchAddressPolicy());
        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseProxy);
        Assert.Equal(DecompressionMethods.All, handler.AutomaticDecompression);
        Assert.NotNull(handler.ConnectCallback);
    }
}
