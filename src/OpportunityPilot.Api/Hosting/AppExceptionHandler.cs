using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OpportunityPilot.Application.Common;

namespace OpportunityPilot.Api.Hosting;

/// <summary>Turns application exceptions into ProblemDetails. Unexpected errors never expose internals.</summary>
public sealed class AppExceptionHandler(IProblemDetailsService problemDetails, ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        ProblemDetails problem = exception switch
        {
            RequestValidationException v => new ValidationProblemDetails(v.Errors) { Status = 400, Title = "Validation failed" },
            NotFoundException n => new ProblemDetails { Status = 404, Title = "Not found", Detail = n.Message },
            ConflictException c => new ProblemDetails { Status = 409, Title = "Conflict", Detail = c.Message },
            UnauthorizedAccessException => new ProblemDetails { Status = 401, Title = "Unauthorized" },
            BadHttpRequestException b => new ProblemDetails { Status = b.StatusCode, Title = "Bad request" },
            _ => new ProblemDetails { Status = 500, Title = "Unexpected error", Detail = "The error was logged with the correlation id below." }
        };

        if (problem.Status == 500) logger.LogError(exception, "Unhandled exception");

        context.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem, Exception = exception });
    }
}
