using OpportunityPilot.Infrastructure.Research;

namespace OpportunityPilot.UnitTests.Research;

public class ContentParserTests
{
    private readonly ContentParser _parser = new();

    [Fact]
    public void Html_keeps_title_and_body_text_and_drops_scripts_styles_and_page_chrome()
    {
        var page = _parser.ParseHtml("""
            <html><head><title> Acme  Careers </title><style>p{color:red}</style></head>
            <body>
              <header>Site header</header><nav>Home | Jobs</nav>
              <script>var secret = "token";</script><noscript>Enable JS</noscript>
              <main><h1>Senior Engineer</h1><p>We use <b>C#</b>   and PostgreSQL.</p><ul><li>Pune</li><li>Hybrid</li></ul>
              <svg><text>logo</text></svg></main>
              <footer>© Acme <a href="/contact">Contact</a> <a href="mailto:jobs@acme.example">Mail</a> <a href="/blog">Blog</a></footer>
            </body></html>
            """, "https://acme.example/jobs/1");

        Assert.Equal("Acme Careers", page.Title);
        Assert.Contains("Senior Engineer", page.Text);
        Assert.Contains("We use C# and PostgreSQL.", page.Text);
        Assert.Contains("Pune\nHybrid", page.Text);
        foreach (var dropped in new[] { "secret", "Site header", "Home | Jobs", "Enable JS", "logo", "color:red", "© Acme" })
            Assert.DoesNotContain(dropped, page.Text);
        Assert.Equal(["https://acme.example/contact", "mailto:jobs@acme.example"], page.Links);
    }

    [Fact]
    public void A_javascript_only_page_yields_almost_no_text()
    {
        var page = _parser.ParseHtml("<html><body><div id=root></div><script>render()</script></body></html>", null);
        Assert.True(page.Text.Length < 200);
    }

    [Fact]
    public void Rss_items_become_entries_with_text_and_dates()
    {
        var feed = _parser.ParseFeed("""
            <?xml version="1.0"?>
            <rss version="2.0"><channel><title>Acme Jobs</title>
              <item><title>Backend Engineer</title><link>https://acme.example/jobs/1</link>
                <description>&lt;p&gt;C# and &lt;b&gt;Docker&lt;/b&gt;&lt;/p&gt;</description>
                <pubDate>Tue, 29 Sep 2026 10:00:00 GMT</pubDate></item>
              <item><title></title><link>javascript:alert(1)</link><description>No title</description></item>
            </channel></rss>
            """);

        Assert.True(feed.IsFeed);
        Assert.Equal("Acme Jobs", feed.Title);
        Assert.Equal(2, feed.Entries.Count);
        var first = feed.Entries[0];
        Assert.Equal("Backend Engineer", first.Title);
        Assert.Equal("https://acme.example/jobs/1", first.Link);
        Assert.Equal("C# and Docker", first.Text);
        Assert.Equal(new DateTime(2026, 9, 29, 10, 0, 0, DateTimeKind.Utc), first.PublishedAt);
        Assert.Null(feed.Entries[1].Link);                 // only http(s) links are kept
        Assert.Equal("Untitled entry", feed.Entries[1].Title);
    }

    [Fact]
    public void Atom_entries_use_the_alternate_link_and_summary()
    {
        var feed = _parser.ParseFeed("""
            <feed xmlns="http://www.w3.org/2005/Atom"><title>News</title>
              <entry><title>Globex raises Series B</title>
                <link rel="self" href="https://news.example/self"/><link rel="alternate" href="https://news.example/globex"/>
                <summary>Globex is hiring in Pune.</summary><updated>2026-09-30T08:00:00Z</updated></entry>
            </feed>
            """);

        var entry = Assert.Single(feed.Entries);
        Assert.Equal("https://news.example/globex", entry.Link);
        Assert.Equal("Globex is hiring in Pune.", entry.Text);
        Assert.NotNull(entry.PublishedAt);
    }

    [Fact]
    public void Feeds_are_capped_at_100_entries()
    {
        var items = string.Concat(Enumerable.Range(1, 150).Select(i => $"<item><title>Job {i}</title></item>"));
        var feed = _parser.ParseFeed($"<rss version=\"2.0\"><channel>{items}</channel></rss>");
        Assert.Equal(100, feed.Entries.Count);
    }

    [Fact]
    public void A_dtd_or_external_entity_is_refused_rather_than_resolved()
    {
        var feed = _parser.ParseFeed("""
            <?xml version="1.0"?>
            <!DOCTYPE rss [<!ENTITY xxe SYSTEM "file:///etc/passwd">]>
            <rss version="2.0"><channel><item><title>&xxe;</title></item></channel></rss>
            """);

        Assert.False(feed.IsFeed);
        Assert.Empty(feed.Entries);
        Assert.Contains("DTD", feed.FailureReason);
    }

    [Theory]
    [InlineData("<html><body>not a feed</body></html>")]
    [InlineData("<rdf>x</rdf>")]
    [InlineData("not xml at all")]
    public void Documents_that_are_not_rss_or_atom_are_reported(string xml) =>
        Assert.False(_parser.ParseFeed(xml).IsFeed);
}
