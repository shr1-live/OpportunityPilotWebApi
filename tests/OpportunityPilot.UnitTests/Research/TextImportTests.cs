using OpportunityPilot.Application.Imports;
using OpportunityPilot.Application.Sources;

namespace OpportunityPilot.UnitTests.Research;

public class TextImportTests
{
    [Fact]
    public void Paste_splits_on_dash_lines_and_reads_labelled_fields()
    {
        var entries = PasteParser.Parse("""

            Senior .NET Engineer
            Company: Acme
            Location: Pune
            URL: https://jobs.example/1
            We use C# and PostgreSQL.

            5+ years.
            ---
            Data Analyst
            company:   Globex
            SQL and Python
            ---

            """);

        Assert.Equal(2, entries.Count);
        var first = entries[0];
        Assert.Equal("Senior .NET Engineer", first.Title);
        Assert.Equal("Acme", first.Company);
        Assert.Equal("Pune", first.Location);
        Assert.Equal("https://jobs.example/1", first.Url);
        Assert.Equal("We use C# and PostgreSQL.\n\n5+ years.", first.Description);
        Assert.Contains("Company: Acme", first.Raw);
        Assert.Equal("Globex", entries[1].Company);
        Assert.Null(entries[1].Location);
    }

    [Fact]
    public void Paste_handles_crlf_website_and_a_title_only_block()
    {
        var entries = PasteParser.Parse("Ledgerly\r\nWebsite: ledgerly.example\r\nCountry: India\r\n---\r\nBare Co");

        Assert.Equal("ledgerly.example", entries[0].Website);
        Assert.Equal("India", entries[0].Country);
        Assert.Equal("Bare Co", entries[1].Title);
        Assert.Equal(string.Empty, entries[1].Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n---\n   ")]
    public void Paste_with_no_content_has_no_entries(string text) => Assert.Empty(PasteParser.Parse(text));

    [Fact]
    public void Paste_keeps_a_label_like_first_line_as_the_title()
    {
        var entry = Assert.Single(PasteParser.Parse("Location: Pune is our office\nCompany: Acme"));
        Assert.Equal("Location: Pune is our office", entry.Title);
        Assert.Equal("Acme", entry.Company);
    }

    [Fact]
    public void Csv_reads_quotes_escaped_quotes_commas_and_newlines()
    {
        Assert.True(Csv.TryParse("title,company,description\r\n\"Dev, Senior\",Acme,\"He said \"\"hi\"\"\r\nsecond line\"\r\nQA,Globex,\r\n", out var rows, out _));

        Assert.Equal(3, rows.Count);
        Assert.Equal(["Dev, Senior", "Acme", "He said \"hi\"\r\nsecond line"], rows[1]);
        Assert.Equal(["QA", "Globex", ""], rows[2]);
    }

    [Fact]
    public void Csv_ignores_a_bom_blank_lines_and_trailing_newline_and_accepts_lf()
    {
        Assert.True(Csv.TryParse("\uFEFFname,website\n\nAcme,acme.example\n", out var rows, out _));
        Assert.Equal(2, rows.Count);
        Assert.Equal("name", rows[0][0]);
        Assert.Equal(["Acme", "acme.example"], rows[1]);
    }

    [Fact]
    public void Csv_without_final_newline_and_with_padded_quoted_field()
    {
        Assert.True(Csv.TryParse("a,b\n \"x\" , y", out var rows, out _));
        Assert.Equal(["x", "y"], rows[1]);
    }

    [Fact]
    public void Csv_with_an_unclosed_quote_is_rejected()
    {
        Assert.False(Csv.TryParse("a,b\n\"never closed,1\n", out _, out var error));
        Assert.Contains("not closed", error);
    }

    [Theory]
    [InlineData("=HYPERLINK(\"http://evil\")", "\"'=HYPERLINK(\"\"http://evil\"\")\"")]
    [InlineData("+1+1", "\"'+1+1\"")]
    [InlineData("-2", "\"'-2\"")]
    [InlineData("@SUM(A1)", "\"'@SUM(A1)\"")]
    [InlineData("\tcmd", "\"'\tcmd\"")]
    [InlineData("\rcmd", "\"'\rcmd\"")]
    [InlineData("Plain, text", "\"Plain, text\"")]
    [InlineData(null, "\"\"")]
    public void Exported_cells_are_quoted_and_formula_prefixes_neutralised(string? value, string expected) =>
        Assert.Equal(expected, Csv.Cell(value));

    [Fact]
    public void Exported_rows_round_trip_through_the_parser()
    {
        var line = Csv.Row(["a,b", "say \"x\"", "multi\nline"]);
        Assert.True(Csv.TryParse(line, out var rows, out _));
        Assert.Equal(["a,b", "say \"x\"", "multi\nline"], Assert.Single(rows));
    }

    [Theory]
    [InlineData("acme.example", "https://acme.example")]
    [InlineData("http://acme.example/x", "http://acme.example/x")]
    [InlineData("not a site", null)]
    [InlineData("ftp://acme.example", null)]
    public void Websites_are_normalised(string input, string? expected) =>
        Assert.Equal(expected, ImportService.NormaliseWebsite(input));
}
