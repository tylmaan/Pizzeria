using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pizzeria.Api.IntegrationTests.Infrastructure;
using Pizzeria.Application.Locations;
using Pizzeria.Application.Platform;
using Pizzeria.Application.Tenancy;
using Pizzeria.Application.Users;
using Pizzeria.Domain.Identity;
using Pizzeria.Domain.Tenancy;
using Pizzeria.Infrastructure.Persistence;

namespace Pizzeria.Api.IntegrationTests;

/// <summary>A user of company A must never see or change data of company B.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class TenantIsolationTests(PizzeriaApiFactory factory)
{
    [Fact]
    public async Task CompanyAdmin_SeesOnlyOwnLocations()
    {
        var (a, b) = await CreateTwoCompaniesAsync();
        var locationA = await a.Admin.CreateLocationAsync("A Centrum");
        var locationB = await b.Admin.CreateLocationAsync("B Centrum");

        var locations = await a.Admin.GetFromJsonAsync<List<LocationDto>>("/api/locations");

        Assert.Equal([locationA.Id], locations!.Select(l => l.Id));
        Assert.DoesNotContain(locations!, l => l.Id == locationB.Id);
    }

    [Fact]
    public async Task CompanyAdmin_CannotReadOrUpdateOtherCompanysLocation()
    {
        var (a, b) = await CreateTwoCompaniesAsync();
        var locationB = await b.Admin.CreateLocationAsync("B Centrum");

        using var get = await a.Admin.GetAsync(new Uri($"/api/locations/{locationB.Id}", UriKind.Relative));
        using var put = await a.Admin.PutAsJsonAsync(
            $"/api/locations/{locationB.Id}",
            new UpdateLocationRequest("Hacked", locationB.Address, "Europe/Warsaw", false));

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        var unchanged = await b.Admin.GetFromJsonAsync<LocationDto>($"/api/locations/{locationB.Id}");
        Assert.Equal("B Centrum", unchanged!.Name);
        Assert.True(unchanged.IsActive);
    }

    [Fact]
    public async Task CompanyAdmin_CannotAssignUserToOtherCompanysLocation()
    {
        var (a, b) = await CreateTwoCompaniesAsync();
        var locationB = await b.Admin.CreateLocationAsync("B Centrum");

        using var response = await a.Admin.PostAsJsonAsync(
            "/api/users",
            new CreateUserRequest("intruder@test.local", PizzeriaApiFactory.Password, "Intruder", Roles.Staff, [locationB.Id]));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("location.not_found", await AuthTests.ReadCodeAsync(response));
    }

    [Fact]
    public async Task CompanyAdmin_SeesOnlyOwnUsers()
    {
        var (a, b) = await CreateTwoCompaniesAsync();
        var locationB = await b.Admin.CreateLocationAsync("B Centrum");
        var staffB = await b.Admin.CreateUserAsync(Roles.Staff, locationB.Id);

        var users = await a.Admin.GetFromJsonAsync<List<UserDto>>("/api/users");

        Assert.Equal([a.AdminEmail], users!.Select(u => u.Email));
        Assert.DoesNotContain(users!, u => u.Email == staffB || u.Email == b.AdminEmail);
    }

    [Fact]
    public async Task Staff_SeesOnlyAssignedLocationsOfOwnCompany()
    {
        var (a, _) = await CreateTwoCompaniesAsync();
        var assigned = await a.Admin.CreateLocationAsync("A Centrum");
        var other = await a.Admin.CreateLocationAsync("A Rynek");
        var staffEmail = await a.Admin.CreateUserAsync(Roles.Staff, assigned.Id);
        using var staff = await factory.LoginAsync(staffEmail);

        var locations = await staff.GetFromJsonAsync<List<LocationDto>>("/api/locations");
        using var otherResponse = await staff.GetAsync(new Uri($"/api/locations/{other.Id}", UriKind.Relative));

        Assert.Equal([assigned.Id], locations!.Select(l => l.Id));
        Assert.Equal(HttpStatusCode.NotFound, otherResponse.StatusCode);
    }

    [Fact]
    public async Task LocationManager_CannotCreateCompanyAdmin()
    {
        var (a, _) = await CreateTwoCompaniesAsync();
        var location = await a.Admin.CreateLocationAsync("A Centrum");
        var managerEmail = await a.Admin.CreateUserAsync(Roles.LocationManager, location.Id);
        using var manager = await factory.LoginAsync(managerEmail);

        using var response = await manager.PostAsJsonAsync(
            "/api/users",
            new CreateUserRequest("boss@test.local", PizzeriaApiFactory.Password, "Boss", Roles.CompanyAdmin, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("user.role_not_assignable", await AuthTests.ReadCodeAsync(response));
    }

    [Fact]
    public async Task LocationManager_AssignableRoles_AreOperationalStaffNeedingLocations()
    {
        var (a, _) = await CreateTwoCompaniesAsync();
        var location = await a.Admin.CreateLocationAsync("A Centrum");
        var managerEmail = await a.Admin.CreateUserAsync(Roles.LocationManager, location.Id);
        using var manager = await factory.LoginAsync(managerEmail);

        var roles = await manager.GetFromJsonAsync<List<AssignableRoleDto>>("/api/users/assignable-roles");

        Assert.Equal(
            [new(Roles.Staff, true), new(Roles.Kitchen, true), new(Roles.Driver, true)],
            roles!);
    }

    [Fact]
    public async Task CompanyAdmin_CannotUsePlatformEndpoints()
    {
        var (a, _) = await CreateTwoCompaniesAsync();

        using var response = await a.Admin.GetAsync(new Uri("/api/platform/companies", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PlatformAdmin_SeesAllCompanies()
    {
        var (a, b) = await CreateTwoCompaniesAsync();
        using var platform = await factory.LoginAsync(PizzeriaApiFactory.PlatformAdminEmail);

        var companies = await platform.GetFromJsonAsync<List<CompanyDto>>("/api/platform/companies");

        Assert.Contains(companies!, c => c.Id == a.Company.Id);
        Assert.Contains(companies!, c => c.Id == b.Company.Id);
    }

    [Fact]
    public async Task QueryFilter_HidesOtherTenantsAndEverythingWithoutTenant()
    {
        var (a, b) = await CreateTwoCompaniesAsync();
        var locationA = await a.Admin.CreateLocationAsync("A Centrum");
        await b.Admin.CreateLocationAsync("B Centrum");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // No tenant resolved: nothing is visible.
        Assert.Empty(await db.Locations.ToListAsync());
        Assert.Empty(await db.Companies.ToListAsync());

        scope.ServiceProvider.GetRequiredService<TenantContext>().SetSystemForCompany(a.Company.Id);
        Assert.Equal([locationA.Id], await db.Locations.Select(l => l.Id).ToListAsync());
        Assert.Equal([a.Company.Id], await db.Companies.Select(c => c.Id).ToListAsync());
    }

    [Fact]
    public async Task SaveChanges_BlocksWritingIntoOtherTenant()
    {
        var (a, b) = await CreateTwoCompaniesAsync();

        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetSystemForCompany(a.Company.Id);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Locations.Add(Location.Create(
            b.Company.Id,
            "Smuggled",
            new Address("ul. Obca 1", "00-001", "Warszawa"),
            null,
            DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    private async Task<(TestCompany A, TestCompany B)> CreateTwoCompaniesAsync() =>
        (await factory.CreateCompanyAsync("alpha"), await factory.CreateCompanyAsync("beta"));
}
