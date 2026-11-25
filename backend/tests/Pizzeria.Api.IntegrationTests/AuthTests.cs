using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Pizzeria.Api.Endpoints;
using Pizzeria.Api.IntegrationTests.Infrastructure;
using Pizzeria.Domain.Identity;

namespace Pizzeria.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class AuthTests(PizzeriaApiFactory factory)
{
    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokensAndMeReflectsTenant()
    {
        var tenant = await factory.CreateCompanyAsync("auth");

        var me = await tenant.Admin.GetFromJsonAsync<MeResponse>("/api/auth/me");

        Assert.Equal(tenant.AdminEmail, me!.Email);
        Assert.Equal(tenant.Company.Id, me.CompanyId);
        Assert.Equal([Roles.CompanyAdmin], me.Roles);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401WithCode()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = PizzeriaApiFactory.PlatformAdminEmail, password = "wrong-password-1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("auth.invalid_credentials", await ReadCodeAsync(response));
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401WithCode()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/locations", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("auth.unauthorized", await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Refresh_ValidRefreshToken_IssuesWorkingAccessToken()
    {
        using var client = factory.CreateClient();
        using var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = PizzeriaApiFactory.PlatformAdminEmail, password = PizzeriaApiFactory.Password });
        var tokens = await login.Content.ReadFromJsonAsync<AccessTokenResponse>();

        using var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens!.RefreshToken });
        refresh.EnsureSuccessStatusCode();
        var refreshed = await refresh.Content.ReadFromJsonAsync<AccessTokenResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshed!.AccessToken);
        var me = await client.GetFromJsonAsync<MeResponse>("/api/auth/me");
        Assert.Contains(Roles.PlatformAdmin, me!.Roles);
    }

    [Fact]
    public async Task Refresh_GarbageToken_Returns401()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = "not-a-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    internal static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}
