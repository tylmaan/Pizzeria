using System.Net;
using System.Net.Http.Json;
using Pizzeria.Api.IntegrationTests.Infrastructure;
using Pizzeria.Application.Delivery;
using Pizzeria.Application.Locations;
using Pizzeria.Domain.Identity;

namespace Pizzeria.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class DeliveryTests(PizzeriaApiFactory factory)
{
    // Each test uses its own area (latitude offset) so zones of parallel test companies never matter.
    private static int _nextArea;
    private readonly double _lat = 50 + Interlocked.Increment(ref _nextArea) * 0.5;
    private const double Lon = 20;

    [Fact]
    public async Task Check_AddressInsideZone_ReturnsTermsOfThatZoneAndLocation()
    {
        var tenant = await factory.CreateCompanyAsync("zone");
        var location = await tenant.Admin.CreateLocationAsync("Centrum");
        var zone = await CreateZoneAsync(tenant.Admin, location.Id, "Centrum", Square(_lat, Lon, 0.02), fee: 6.5m, minOrder: 40m, eta: 35);

        var quote = await CheckAsync(tenant.Admin, factory.Geocoder.Place(_lat + 0.01, Lon));

        Assert.True(quote.Delivers);
        Assert.Equal((location.Id, "Centrum", zone.Id), (quote.LocationId!.Value, quote.LocationName, quote.ZoneId!.Value));
        Assert.Equal((6.5m, 40m, 35), (quote.DeliveryFee!.Value, quote.MinOrderValue!.Value, quote.EstimatedMinutes!.Value));
    }

    [Fact]
    public async Task Check_AddressOutsideAllZones_DoesNotDeliver()
    {
        var tenant = await factory.CreateCompanyAsync("zone");
        var location = await tenant.Admin.CreateLocationAsync("Centrum");
        await CreateZoneAsync(tenant.Admin, location.Id, "Centrum", Square(_lat, Lon, 0.02));

        var quote = await CheckAsync(tenant.Admin, factory.Geocoder.Place(_lat + 0.1, Lon));

        Assert.False(quote.Delivers);
        Assert.Null(quote.LocationId);
    }

    [Fact]
    public async Task Check_OverlappingZonesOfTwoLocations_PicksTheSmallerZone()
    {
        var tenant = await factory.CreateCompanyAsync("zone");
        var north = await tenant.Admin.CreateLocationAsync("Północ");
        var center = await tenant.Admin.CreateLocationAsync("Centrum");
        await CreateZoneAsync(tenant.Admin, north.Id, "Całe miasto", Square(_lat, Lon, 0.1), fee: 12m);
        await CreateZoneAsync(tenant.Admin, center.Id, "Rynek", Square(_lat, Lon, 0.01), fee: 0m);

        var quote = await CheckAsync(tenant.Admin, factory.Geocoder.Place(_lat, Lon));

        Assert.Equal((center.Id, 0m), (quote.LocationId!.Value, quote.DeliveryFee!.Value));
    }

    [Fact]
    public async Task Check_ZoneOfInactiveLocationIsIgnored()
    {
        var tenant = await factory.CreateCompanyAsync("zone");
        var location = await tenant.Admin.CreateLocationAsync("Zamknięty");
        await CreateZoneAsync(tenant.Admin, location.Id, "Centrum", Square(_lat, Lon, 0.02));
        using var deactivate = await tenant.Admin.PutAsJsonAsync($"/api/locations/{location.Id}",
            new UpdateLocationRequest(location.Name, location.Address, location.TimeZoneId, false));

        var quote = await CheckAsync(tenant.Admin, factory.Geocoder.Place(_lat, Lon));

        Assert.False(quote.Delivers);
    }

    [Fact]
    public async Task Check_NeverUsesZonesOfAnotherCompany()
    {
        var a = await factory.CreateCompanyAsync("zonea");
        var b = await factory.CreateCompanyAsync("zoneb");
        var locationA = await a.Admin.CreateLocationAsync("A");
        await CreateZoneAsync(a.Admin, locationA.Id, "A", Square(_lat, Lon, 0.05));

        var quote = await CheckAsync(b.Admin, factory.Geocoder.Place(_lat, Lon));

        Assert.False(quote.Delivers);
    }

    [Fact]
    public async Task Check_AnonymousShopResolvesCompanyFromHost()
    {
        var tenant = await factory.CreateCompanyAsync("shop");
        var location = await tenant.Admin.CreateLocationAsync("Centrum");
        await CreateZoneAsync(tenant.Admin, location.Id, "Centrum", Square(_lat, Lon, 0.02));
        using var shop = factory.CreateClient();
        shop.DefaultRequestHeaders.Host = $"{tenant.Company.Slug}.localhost";

        var quote = await CheckAsync(shop, factory.Geocoder.Place(_lat, Lon));

        Assert.Equal(location.Id, quote.LocationId);
    }

    [Fact]
    public async Task Check_AnonymousWithoutShopHost_Returns404()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/delivery/check",
            new DeliveryCheckRequest(factory.Geocoder.Place(_lat, Lon), "00-001", "Warszawa"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("delivery.shop_not_found", await AuthTests.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Check_UnknownAddress_Returns422()
    {
        var tenant = await factory.CreateCompanyAsync("zone");

        using var response = await tenant.Admin.PostAsJsonAsync("/api/delivery/check",
            new DeliveryCheckRequest("ul. Nieistniejąca 1", "00-001", "Warszawa"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("delivery.address_not_found", await AuthTests.ReadCodeAsync(response));
    }

    [Fact]
    public async Task LocationManager_ManagesOnlyZonesOfOwnLocation()
    {
        var tenant = await factory.CreateCompanyAsync("zone");
        var own = await tenant.Admin.CreateLocationAsync("Własny");
        var other = await tenant.Admin.CreateLocationAsync("Cudzy");
        using var manager = await factory.LoginAsync(await tenant.Admin.CreateUserAsync(Roles.LocationManager, own.Id));

        var created = await CreateZoneAsync(manager, own.Id, "Moja", Square(_lat, Lon, 0.02));
        using var foreign = await manager.PostAsJsonAsync($"/api/locations/{other.Id}/delivery-zones", Request("Nie moja", Square(_lat, Lon, 0.02)));
        var listed = await manager.GetFromJsonAsync<List<DeliveryZoneDto>>($"/api/locations/{own.Id}/delivery-zones");

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal([created.Id], listed!.Select(z => z.Id));
    }

    [Fact]
    public async Task Zone_UpdateChangesBoundaryAndTerms()
    {
        var tenant = await factory.CreateCompanyAsync("zone");
        var location = await tenant.Admin.CreateLocationAsync("Centrum");
        var zone = await CreateZoneAsync(tenant.Admin, location.Id, "Centrum", Square(_lat, Lon, 0.02));
        var farAway = factory.Geocoder.Place(_lat + 0.08, Lon);

        using var response = await tenant.Admin.PutAsJsonAsync($"/api/locations/{location.Id}/delivery-zones/{zone.Id}",
            Request("Duża", Square(_lat, Lon, 0.1), fee: 9m));
        var quote = await CheckAsync(tenant.Admin, farAway);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((zone.Id, 9m), (quote.ZoneId!.Value, quote.DeliveryFee!.Value));
    }

    [Fact]
    public async Task Location_IsGeocodedOnCreateAndAddressChange()
    {
        var tenant = await factory.CreateCompanyAsync("zone");
        var first = factory.Geocoder.Place(_lat, Lon);
        var moved = factory.Geocoder.Place(_lat + 0.2, Lon);

        using var created = await tenant.Admin.PostAsJsonAsync("/api/locations",
            new CreateLocationRequest("Centrum", new AddressDto(first, "00-001", "Warszawa"), null));
        var location = (await created.Content.ReadFromJsonAsync<LocationDto>())!;
        using var updated = await tenant.Admin.PutAsJsonAsync($"/api/locations/{location.Id}",
            new UpdateLocationRequest("Centrum", new AddressDto(moved, "00-001", "Warszawa"), "Europe/Warsaw", true));
        var relocated = (await updated.Content.ReadFromJsonAsync<LocationDto>())!;

        Assert.Equal(new GeoPointDto(_lat, Lon), location.Position);
        Assert.Equal(new GeoPointDto(_lat + 0.2, Lon), relocated.Position);
    }

    [Fact]
    public async Task Zone_SelfIntersectingBoundary_Returns422()
    {
        var tenant = await factory.CreateCompanyAsync("zone");
        var location = await tenant.Admin.CreateLocationAsync("Centrum");
        GeoPointDto[] bowTie = [new(_lat, Lon), new(_lat + 0.02, Lon + 0.02), new(_lat, Lon + 0.02), new(_lat + 0.02, Lon)];

        using var response = await tenant.Admin.PostAsJsonAsync($"/api/locations/{location.Id}/delivery-zones", Request("Zła", bowTie));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("delivery.invalid_boundary", await AuthTests.ReadCodeAsync(response));
    }

    private static SaveDeliveryZoneRequest Request(string name, GeoPointDto[] boundary, decimal fee = 5m, decimal minOrder = 30m, int eta = 45) =>
        new(name, boundary, fee, minOrder, eta, IsActive: true);

    private static async Task<DeliveryZoneDto> CreateZoneAsync(
        HttpClient client, Guid locationId, string name, GeoPointDto[] boundary, decimal fee = 5m, decimal minOrder = 30m, int eta = 45)
    {
        using var response = await client.PostAsJsonAsync($"/api/locations/{locationId}/delivery-zones", Request(name, boundary, fee, minOrder, eta));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DeliveryZoneDto>())!;
    }

    private static async Task<DeliveryQuoteDto> CheckAsync(HttpClient client, string street)
    {
        using var response = await client.PostAsJsonAsync("/api/delivery/check", new DeliveryCheckRequest(street, "00-001", "Warszawa"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DeliveryQuoteDto>())!;
    }

    private static GeoPointDto[] Square(double lat, double lon, double half) =>
    [
        new(lat - half, lon - half),
        new(lat - half, lon + half),
        new(lat + half, lon + half),
        new(lat + half, lon - half),
    ];
}
