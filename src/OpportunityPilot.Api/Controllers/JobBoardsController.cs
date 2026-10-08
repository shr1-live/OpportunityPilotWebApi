using Microsoft.AspNetCore.Mvc;
using OpportunityPilot.Application.Common;
using OpportunityPilot.Application.JobBoards;

namespace OpportunityPilot.Api.Controllers;

/// <summary>Live job-board postings (Indeed, LinkedIn, SEEK) through a licensed aggregator. Read-only: nothing is stored or applied to.</summary>
[ApiController]
[Route("api/v1/jobboards")]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(Hosting.RateLimits.JobBoardSearch)]
public sealed class JobBoardsController(IJobBoardSearch search) : ControllerBase
{
    [HttpGet("status")]
    public ActionResult<object> Status() => Ok(new { configured = search.Configured, boards = Enum.GetNames<JobBoard>() });

    [HttpGet("jobs")]
    public async Task<ActionResult<JobBoardSearchResult>> Jobs(
        [FromQuery] string query, [FromQuery] JobBoard board = JobBoard.Indeed, [FromQuery] string? location = null,
        [FromQuery] bool remoteOnly = false, [FromQuery] string datePosted = "week", [FromQuery] string? country = null,
        [FromQuery] int page = 1, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(query) || query.Length > 200) errors["query"] = ["Enter a search of 1–200 characters."];
        if (location is { Length: > 100 }) errors["location"] = ["Location is at most 100 characters."];
        if (!JsearchBoardParser.DatePostedValues.Contains(datePosted)) errors["datePosted"] = ["Use all, today, 3days, week or month."];
        if (country is not null && (country.Length != 2 || !country.All(char.IsAsciiLetter))) errors["country"] = ["Use a two-letter country code."];
        else if (board == JobBoard.Seek && country is not null && !JsearchBoardParser.SeekCountries.Contains(country.ToLowerInvariant()))
            errors["country"] = ["SEEK runs only in Australia (au) and New Zealand (nz)."];
        if (page is < 1 or > 10) errors["page"] = ["Page is 1–10."];
        if (errors.Count > 0) throw new RequestValidationException(errors);
        return Ok(await search.SearchAsync(new(board, query.Trim(), location?.Trim(), remoteOnly, datePosted, country, page), ct));
    }
}

/// <summary>The first W10 route, kept so the Indeed tab and any bookmarks keep working.</summary>
[ApiController]
[Route("api/v1/indeed")]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(Hosting.RateLimits.JobBoardSearch)]
public sealed class IndeedController(IJobBoardSearch search) : ControllerBase
{
    [HttpGet("jobs")]
    public Task<ActionResult<JobBoardSearchResult>> Jobs(
        [FromQuery] string query, [FromQuery] string? location = null, [FromQuery] bool remoteOnly = false,
        [FromQuery] string datePosted = "week", [FromQuery] string? country = null, [FromQuery] int page = 1,
        CancellationToken ct = default) =>
        new JobBoardsController(search) { ControllerContext = ControllerContext }
            .Jobs(query, JobBoard.Indeed, location, remoteOnly, datePosted, country, page, ct);
}
