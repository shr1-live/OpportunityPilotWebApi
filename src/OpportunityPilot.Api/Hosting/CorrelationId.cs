using System.Text.RegularExpressions;

namespace OpportunityPilot.Api.Hosting;

public static partial class CorrelationId
{
    public const string Header = "X-Correlation-ID";

    /// <summary>Accepts a safe inbound id or generates one; echoes it on the response and uses it as the trace identifier.</summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        var inbound = context.Request.Headers[Header].ToString();
        var id = SafeId().IsMatch(inbound) ? inbound : Guid.NewGuid().ToString("N");
        context.TraceIdentifier = id;
        context.Response.Headers[Header] = id;
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Request");
        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id }))
            await next();
    });

    [GeneratedRegex("^[A-Za-z0-9-]{8,64}$")]
    private static partial Regex SafeId();
}
