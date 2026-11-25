using System.Globalization;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Pizzeria.Api.Auth;
using Pizzeria.Api.Endpoints;
using Pizzeria.Api.Errors;
using Pizzeria.Api.Tenancy;
using Pizzeria.Application;
using Pizzeria.Infrastructure;
using Pizzeria.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

// Strict contracts: missing constructor parameters and nulls in non-nullable properties are rejected.
// The OpenAPI document reflects the same rules, so generated TypeScript types have required, non-null fields.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.RespectNullableAnnotations = true;
    options.SerializerOptions.RespectRequiredConstructorParameters = true;
    // Enums travel as names ("MoreExpensiveHalf"), which keeps them readable and stable in TypeScript.
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsCodes.AddDefaultCode);
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddHealthChecks();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration).AddSignInManager();
builder.Services.Configure<TenancyOptions>(builder.Configuration.GetSection(TenancyOptions.SectionName));

builder.Services
    .AddAuthentication(IdentityConstants.BearerScheme)
    .AddBearerToken(IdentityConstants.BearerScheme, options =>
    {
        options.BearerTokenExpiration = TimeSpan.FromMinutes(30);
        options.RefreshTokenExpiration = TimeSpan.FromDays(14);
    });
AuthorizationPolicies.Configure(builder.Services.AddAuthorizationBuilder());

// Brute-force protection on top of Identity lockout: login attempts per client IP per minute.
var loginAttemptsPerMinute = builder.Configuration.GetValue("RateLimiting:LoginAttemptsPerMinute", 10);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthEndpoints.LoginRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = loginAttemptsPerMinute, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

// Request logging wraps the exception handler, so it logs the final status code (e.g. 400, not 500).
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

app.MapHealthChecks("/health").AllowAnonymous();

var api = app.MapGroup("/api");
api.MapAuthEndpoints();
api.MapPlatformEndpoints();
api.MapLocationEndpoints();
api.MapUserEndpoints();
api.MapMenuEndpoints();

await DatabaseInitializer.InitializeAsync(app.Services).ConfigureAwait(false);

await app.RunAsync().ConfigureAwait(false);
