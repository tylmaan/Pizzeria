using System.Globalization;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Pizzeria.Api.Auth;
using Pizzeria.Api.Background;
using Pizzeria.Api.Endpoints;
using Pizzeria.Api.Errors;
using Pizzeria.Api.Realtime;
using Pizzeria.Api.Tenancy;
using Pizzeria.Application;
using Pizzeria.Application.Drivers;
using Pizzeria.Application.Orders;
using Pizzeria.Application.Printing;
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
    // Numbers are numbers: no "12.50" strings accepted, so OpenAPI (and TypeScript) types are plain `number`.
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    // Enums travel as names ("MoreExpensiveHalf"), which keeps them readable and stable in TypeScript.
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsCodes.AddDefaultCode);
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddHealthChecks();

// Behind a reverse proxy: real client IP (rate limits), scheme and host (shop subdomain).
// Only loopback proxies are trusted by default; production hosting must add its proxy network.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration).AddSignInManager();
builder.Services.Configure<TenancyOptions>(builder.Configuration.GetSection(TenancyOptions.SectionName));
builder.Services.AddScoped<TenantResolver>();

builder.Services.AddSignalR(options => options.AddFilter<HubTenantFilter>())
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Replaces the no-op notifier registered by AddApplication.
builder.Services.AddSingleton<IOrderNotifier, SignalROrderNotifier>();
builder.Services.AddSingleton<IDriverPositionNotifier, SignalRDriverPositionNotifier>();
builder.Services.AddSingleton<IPrintJobNotifier, SignalRPrintJobNotifier>();

builder.Services.Configure<DriverTrackingOptions>(builder.Configuration.GetSection(DriverTrackingOptions.SectionName));
builder.Services.AddSingleton<DriverPositionRetention>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DriverPositionRetention>());

builder.Services
    .AddAuthentication(IdentityConstants.BearerScheme)
    .AddBearerToken(IdentityConstants.BearerScheme, options =>
    {
        options.BearerTokenExpiration = TimeSpan.FromMinutes(30);
        options.RefreshTokenExpiration = TimeSpan.FromDays(14);
        // Browsers cannot set headers on WebSocket requests: SignalR sends the token in the query string.
        options.Events = new BearerTokenEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Path.StartsWithSegments(OrdersHub.Path)
                    && context.Request.Query["access_token"] is { Count: > 0 } token)
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            },
        };
    })
    .AddScheme<AuthenticationSchemeOptions, AgentTokenAuthenticationHandler>(AgentTokenAuthenticationHandler.SchemeName, null);
AuthorizationPolicies.Configure(builder.Services.AddAuthorizationBuilder());

// Brute-force protection on top of Identity lockout: login attempts per client IP per minute.
var loginAttemptsPerMinute = builder.Configuration.GetValue("RateLimiting:LoginAttemptsPerMinute", 10);
var deliveryChecksPerMinute = builder.Configuration.GetValue("RateLimiting:DeliveryChecksPerMinute", 30);
var ordersPerMinute = builder.Configuration.GetValue("RateLimiting:OrdersPerMinute", 10);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthEndpoints.LoginRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = loginAttemptsPerMinute, Window = TimeSpan.FromMinutes(1) }));
    options.AddPolicy(DeliveryEndpoints.DeliveryCheckRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = deliveryChecksPerMinute, Window = TimeSpan.FromMinutes(1) }));
    options.AddPolicy(ShopEndpoints.PlaceOrderRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = ordersPerMinute, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

app.UseForwardedHeaders();

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
api.MapDeliveryEndpoints();
api.MapShopEndpoints();
api.MapShopAccountEndpoints();
api.MapOrderEndpoints();
api.MapDriverEndpoints();
api.MapPaymentEndpoints(builder.Configuration);
api.MapReportEndpoints();
api.MapPrintingEndpoints();

// Anonymous connections are allowed (customer tracking); hub methods check access themselves.
app.MapHub<OrdersHub>(OrdersHub.Path).AllowAnonymous();
app.MapHub<AgentHub>(AgentHub.Path).RequireAuthorization(AuthorizationPolicies.Agent);

await DatabaseInitializer.InitializeAsync(app.Services).ConfigureAwait(false);

await app.RunAsync().ConfigureAwait(false);
