using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Pizzeria.Api.Errors;
using Pizzeria.Api.IntegrationTests.Infrastructure;
using Pizzeria.Api.Tenancy;
using Pizzeria.Application.Locations;

namespace Pizzeria.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class ValidationTests(PizzeriaApiFactory factory)
{
    [Fact]
    public async Task CreateLocation_InvalidInput_Returns400WithFieldErrorCodes()
    {
        var tenant = await factory.CreateCompanyAsync("valid");

        using var response = await tenant.Admin.PostAsJsonAsync(
            "/api/locations",
            new CreateLocationRequest("", new AddressDto("ul. Testowa 1", "12345", "Warszawa"), "Mars/Olympus"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal("common.validation_failed", root.GetProperty("code").GetString());
        var errors = root.GetProperty("errors");
        Assert.Equal("validation.not_empty", errors.GetProperty("name")[0].GetString());
        Assert.Equal("validation.postal_code", errors.GetProperty("address.postalCode")[0].GetString());
        Assert.Equal("validation.time_zone", errors.GetProperty("timeZoneId")[0].GetString());
    }
}

public sealed class ShopTenantResolutionTests
{
    [Theory]
    [InlineData("dominos.localhost", "localhost", "dominos")]
    [InlineData("Dominos.Platforma.pl", "platforma.pl", "dominos")]
    [InlineData("localhost", "localhost", null)]
    [InlineData("a.b.localhost", "localhost", null)]
    [InlineData("dominos.example.com", "platforma.pl", null)]
    [InlineData("evilplatforma.pl", "platforma.pl", null)]
    public void FindShopSlug_ExtractsSingleLabelSubdomain(string host, string root, string? expected)
    {
        Assert.Equal(expected, TenantResolutionMiddleware.FindShopSlug(host, root));
    }

    [Theory]
    [InlineData("NotEmptyValidator", "validation.not_empty")]
    [InlineData("MaximumLengthValidator", "validation.maximum_length")]
    [InlineData("validation.postal_code", "validation.postal_code")]
    public void ForValidator_MapsFluentValidationCodes(string input, string expected)
    {
        Assert.Equal(expected, ProblemDetailsCodes.ForValidator(input));
    }
}
