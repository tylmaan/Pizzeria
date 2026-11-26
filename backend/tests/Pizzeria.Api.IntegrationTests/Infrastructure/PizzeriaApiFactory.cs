using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Pizzeria.Api.Realtime;
using Pizzeria.Application.Delivery;
using Pizzeria.Application.Orders;
using Testcontainers.PostgreSql;

namespace Pizzeria.Api.IntegrationTests.Infrastructure;

/// <summary>API host backed by a throwaway PostgreSQL + PostGIS container, shared by all tests in the collection.</summary>
public sealed class PizzeriaApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string PlatformAdminEmail = "platform@test.local";

    // Test-only credential for the throwaway database.
    public const string Password = "integration-test-1";

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgis/postgis:17-3.5")
        .Build();

    /// <summary>Replaces the fake geocoder so tests decide where an address lies.</summary>
    public StubGeocoder Geocoder { get; } = new();

    /// <summary>Order notifications sent by the API (still delivered to SignalR clients).</summary>
    public RecordingNotifier? Notifications { get; private set; }

    public async Task InitializeAsync() => await _database.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _database.DisposeAsync();
        await base.DisposeAsync();
    }

    public async Task<HttpClient> LoginAsync(string email, string password = Password)
    {
        var client = CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<AccessTokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Pizzeria", _database.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Seed:PlatformAdminEmail", PlatformAdminEmail);
        builder.UseSetting("Seed:PlatformAdminPassword", Password);
        builder.UseSetting("Seed:DemoData", "false");
        // Every test signs in several users from the same (test server) address.
        builder.UseSetting("RateLimiting:LoginAttemptsPerMinute", "10000");
        builder.UseSetting("RateLimiting:DeliveryChecksPerMinute", "10000");
        builder.UseSetting("RateLimiting:OrdersPerMinute", "10000");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IGeocodingService>(Geocoder);
            services.AddSingleton<SignalROrderNotifier>();
            services.AddSingleton<IOrderNotifier>(sp => Notifications ??= new RecordingNotifier(sp.GetRequiredService<SignalROrderNotifier>()));
        });
    }
}

[CollectionDefinition(Name)]
public sealed class ApiTestGroup : ICollectionFixture<PizzeriaApiFactory>
{
    public const string Name = "api";
}
