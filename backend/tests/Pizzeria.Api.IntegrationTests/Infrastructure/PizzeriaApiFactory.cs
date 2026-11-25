using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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
    }
}

[CollectionDefinition(Name)]
public sealed class ApiTestGroup : ICollectionFixture<PizzeriaApiFactory>
{
    public const string Name = "api";
}
