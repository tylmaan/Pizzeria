using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pizzeria.Application.Delivery;
using Pizzeria.Application.Locations;
using Pizzeria.Application.Menu;

namespace Pizzeria.Api.IntegrationTests.Infrastructure;

/// <summary>A company with a pizza menu, one location with a delivery zone and an anonymous shop client.</summary>
public sealed record TestShop(
    TestCompany Tenant,
    LocationDto Location,
    HttpClient Shop,
    CategoryDto Pizza,
    ModifierGroupDto Sauce,
    ProductDto Margherita,
    ProductDto Pepperoni,
    string StreetInZone,
    string StreetOutsideZone)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public Guid Small => Pizza.Sizes[0].Id;

    public Guid Large => Pizza.Sizes[1].Id;

    public Guid Garlic => Sauce.Modifiers[0].Id;

    /// <summary>
    /// Margherita 25/32 and Pepperoni 30/38 (both allow halves), sauce group (exactly one: garlic 2 zł),
    /// zone around (<paramref name="lat"/>, 20) with delivery 6 zł and minimum 40 zł.
    /// </summary>
    public static async Task<TestShop> CreateAsync(PizzeriaApiFactory factory, double lat)
    {
        var tenant = await factory.CreateCompanyAsync("shop");
        var admin = tenant.Admin;
        var location = await admin.CreateLocationAsync("Centrum");

        var pizza = await PostAsync<CategoryDto>(admin, "/api/menu/categories",
            new SaveCategoryRequest("Pizza", [new(null, "32 cm"), new(null, "42 cm")]));
        var (small, large) = (pizza.Sizes[0].Id, pizza.Sizes[1].Id);
        var sauce = await PostAsync<ModifierGroupDto>(admin, "/api/menu/modifier-groups",
            new SaveModifierGroupRequest(pizza.Id, "Sos", 1, 1, [new(null, "Czosnkowy", true, [new(small, 2m), new(large, 2m)])]));
        var margherita = await PostAsync<ProductDto>(admin, "/api/menu/products",
            new SaveProductRequest(pizza.Id, "Margherita", null, true, true, [new(small, 25m), new(large, 32m)], [sauce.Id]));
        var pepperoni = await PostAsync<ProductDto>(admin, "/api/menu/products",
            new SaveProductRequest(pizza.Id, "Pepperoni", null, true, true, [new(small, 30m), new(large, 38m)], [sauce.Id]));

        GeoPointDto[] square = [new(lat - 0.02, 19.98), new(lat - 0.02, 20.02), new(lat + 0.02, 20.02), new(lat + 0.02, 19.98)];
        await PostAsync<DeliveryZoneDto>(admin, $"/api/locations/{location.Id}/delivery-zones",
            new SaveDeliveryZoneRequest("Centrum", square, 6m, 40m, 40, true));

        var shop = factory.CreateClient();
        shop.DefaultRequestHeaders.Host = $"{tenant.Company.Slug}.localhost";

        return new TestShop(tenant, location, shop, pizza, sauce, margherita, pepperoni,
            factory.Geocoder.Place(lat, 20), factory.Geocoder.Place(lat + 0.3, 20));
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        using var response = await client.PostAsJsonAsync(url, body, Json);
        if (response.StatusCode != HttpStatusCode.Created)
        {
            throw new InvalidOperationException($"{url}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        }

        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }
}
