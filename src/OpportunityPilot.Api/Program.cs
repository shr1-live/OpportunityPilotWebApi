using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Api.Auth;
using OpportunityPilot.Api.Hosting;
using OpportunityPilot.Application;
using OpportunityPilot.Application.Configuration;
using OpportunityPilot.Infrastructure;
using OpportunityPilot.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Render supplies PORT; honour it unless URLs were set explicitly.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.Section));
builder.Services.Configure<FeatureOptions>(builder.Configuration.GetSection(FeatureOptions.Section));
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.Section));

// Missing configuration does not stop the process: affected requests answer 503 "Setup required"
// and /api/v1/capabilities lists the gaps, so a deploy goes green before every secret is entered.
var setup = new SetupState();
builder.Services.AddSingleton(setup);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, setup);
builder.AddOpportunityPilotAuth(setup);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["correlationId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<AppExceptionHandler>();

builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .WithHeaders("Authorization", "Content-Type", CorrelationId.Header, DevBypassAuthenticationHandler.Header)
    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
    .WithExposedHeaders(CorrelationId.Header)));

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.User.FindFirst("sub")?.Value ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

// Schema changes are an explicit release step: `dotnet OpportunityPilot.Api.dll --migrate` applies and exits.
if (args.Contains("--migrate"))
{
    if (!setup.DatabaseConfigured)
    {
        Console.WriteLine("Skipping migrations: ConnectionStrings__Main is not configured.");
        return;
    }
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    Console.WriteLine("Migrations applied.");
    return;
}

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup") && setup.DatabaseConfigured)
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException("Database:MigrateOnStartup is only allowed in Development. Use --migrate as a release step.");
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

foreach (var gap in setup.Missing)
    app.Logger.LogWarning("Setup required: {Gap} Affected requests answer 503 until it is set.", gap);

if (allowedOrigins.Length == 0)
    app.Logger.LogWarning("Cors:AllowedOrigins is empty; browsers on other origins cannot call this API.");

app.UseForwardedHeaders();
app.UseCorrelationId();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
    app.MapOpenApi().AllowAnonymous();

app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
