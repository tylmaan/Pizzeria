using System.Net.Http.Json;
using Pizzeria.Application.Locations;
using Pizzeria.Application.Platform;
using Pizzeria.Application.Users;

namespace Pizzeria.Api.IntegrationTests.Infrastructure;

/// <summary>A company created through the platform API, with its admin signed in.</summary>
public sealed record TestCompany(CompanyDto Company, string AdminEmail, HttpClient Admin);

public static class TestTenants
{
    public static async Task<TestCompany> CreateCompanyAsync(this PizzeriaApiFactory factory, string prefix)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var slug = $"{prefix}-{suffix}";
        var adminEmail = $"admin@{slug}.test";

        using var platform = await factory.LoginAsync(PizzeriaApiFactory.PlatformAdminEmail);
        using var response = await platform.PostAsJsonAsync(
            "/api/platform/companies",
            new CreateCompanyRequest($"{prefix} {suffix}", slug, adminEmail, PizzeriaApiFactory.Password, "Admin"));
        response.EnsureSuccessStatusCode();
        var company = (await response.Content.ReadFromJsonAsync<CompanyDto>())!;

        return new TestCompany(company, adminEmail, await factory.LoginAsync(adminEmail));
    }

    public static async Task<LocationDto> CreateLocationAsync(this HttpClient admin, string name)
    {
        using var response = await admin.PostAsJsonAsync(
            "/api/locations",
            new CreateLocationRequest(name, new AddressDto("ul. Testowa 1", "00-001", "Warszawa"), null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LocationDto>())!;
    }

    public static async Task<string> CreateUserAsync(this HttpClient manager, string role, params Guid[] locationIds)
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.local";
        using var response = await manager.PostAsJsonAsync(
            "/api/users",
            new CreateUserRequest(email, PizzeriaApiFactory.Password, role, role, locationIds));
        response.EnsureSuccessStatusCode();
        return email;
    }
}
