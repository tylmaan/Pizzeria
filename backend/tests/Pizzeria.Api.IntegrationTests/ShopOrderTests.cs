using System.Net;
using System.Net.Http.Json;
using Pizzeria.Api.IntegrationTests.Infrastructure;
using Pizzeria.Application.Menu;
using Pizzeria.Application.Orders;
using Pizzeria.Domain.Orders;

namespace Pizzeria.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class ShopOrderTests(PizzeriaApiFactory factory)
{
    private static int _nextArea;
    private readonly double _lat = 40 + Interlocked.Increment(ref _nextArea) * 0.5;

    [Fact]
    public async Task Shop_ShowsOnlyOrderableMenuAndPickupLocations()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        using var deactivate = await shop.Tenant.Admin.PutAsJsonAsync($"/api/menu/products/{shop.Pepperoni.Id}",
            new SaveProductRequest(shop.Pizza.Id, "Pepperoni", null, true, false, shop.Pepperoni.Prices, shop.Pepperoni.ModifierGroupIds), TestShop.Json);

        var data = await shop.Shop.GetFromJsonAsync<ShopDto>("/api/shop", TestShop.Json);

        Assert.Equal(shop.Tenant.Company.Name, data!.CompanyName);
        Assert.Equal(["Centrum"], data.Locations.Select(l => l.Name));
        Assert.Equal(["Margherita"], data.Menu.Products.Select(p => p.Name));
    }

    [Fact]
    public async Task PlaceDeliveryOrder_PricesOnServerAndIsTrackable()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);

        var placed = await PlaceAsync(shop, Delivery(shop, Item(shop.Margherita.Id, shop.Large, 2, shop.Garlic)));
        var tracking = await shop.Shop.GetFromJsonAsync<OrderTrackingDto>($"/api/shop/orders/{placed.TrackingToken}", TestShop.Json);

        Assert.Equal(1, placed.Number);
        Assert.Equal(74m, placed.Total); // 2 × (32 + 2) + 6 delivery
        Assert.Equal(OrderStatus.New, tracking!.Status);
        Assert.Equal((68m, 6m, 74m), (tracking.ItemsTotal, tracking.DeliveryFee, tracking.Total));
        var item = Assert.Single(tracking.Items);
        Assert.Equal(("Margherita", "42 cm", 2, 34m), (item.ProductName, item.SizeName, item.Quantity, item.UnitPrice));
        Assert.Equal("Czosnkowy", Assert.Single(item.Modifiers).Name);
        Assert.Equal("Centrum", tracking.LocationName);
        Assert.Equal([OrderStatus.New], tracking.History.Select(h => h.Status));
    }

    [Fact]
    public async Task OrderNumbers_IncreasePerLocationAndDay()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);

        var first = await PlaceAsync(shop, Pickup(shop, Item(shop.Margherita.Id, shop.Small, 1, shop.Garlic)));
        var second = await PlaceAsync(shop, Pickup(shop, Item(shop.Margherita.Id, shop.Small, 1, shop.Garlic)));

        Assert.Equal((1, 2), (first.Number, second.Number));
    }

    [Fact]
    public async Task PickupOrder_HasNoDeliveryFeeOrMinimum()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);

        var placed = await PlaceAsync(shop, Pickup(shop, Item(shop.Margherita.Id, shop.Small, 1, shop.Garlic)));

        Assert.Equal(27m, placed.Total);
    }

    [Fact]
    public async Task HalfAndHalf_IsPricedByCompanyRule()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var item = new OrderItemInput(shop.Margherita.Id, shop.Pepperoni.Id, shop.Large, 1, [shop.Garlic], null);

        var placed = await PlaceAsync(shop, Delivery(shop, item));

        Assert.Equal(46m, placed.Total); // max(32, 38) + 2 sauce + 6 delivery
    }

    [Fact]
    public async Task BelowZoneMinimum_Returns422()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);

        using var response = await shop.Shop.PostAsJsonAsync("/api/shop/orders",
            Delivery(shop, Item(shop.Margherita.Id, shop.Small, 1, shop.Garlic)), TestShop.Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("order.below_minimum_value", await AuthTests.ReadCodeAsync(response));
    }

    [Fact]
    public async Task AddressOutsideZones_Returns422()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var request = Delivery(shop, Item(shop.Margherita.Id, shop.Large, 2, shop.Garlic)) with
        {
            DeliveryAddress = new DeliveryAddressInput(shop.StreetOutsideZone, "00-001", "Warszawa", null),
        };

        using var response = await shop.Shop.PostAsJsonAsync("/api/shop/orders", request, TestShop.Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("delivery.address_not_delivered", await AuthTests.ReadCodeAsync(response));
    }

    [Fact]
    public async Task MissingRequiredModifier_Returns422()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);

        using var response = await shop.Shop.PostAsJsonAsync("/api/shop/orders",
            Pickup(shop, Item(shop.Margherita.Id, shop.Small, 1)), TestShop.Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("menu.modifier_selection_invalid", await AuthTests.ReadCodeAsync(response));
    }

    [Fact]
    public async Task ProductOfAnotherCompany_IsUnavailable()
    {
        var a = await TestShop.CreateAsync(factory, _lat);
        var b = await TestShop.CreateAsync(factory, _lat + 0.1);

        using var response = await b.Shop.PostAsJsonAsync("/api/shop/orders",
            Pickup(b, Item(a.Margherita.Id, a.Small, 1, a.Garlic)), TestShop.Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("menu.product_unavailable", await AuthTests.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Tracking_WithAnotherShopsHost_IsNotFound()
    {
        var a = await TestShop.CreateAsync(factory, _lat);
        var b = await TestShop.CreateAsync(factory, _lat + 0.1);
        var placed = await PlaceAsync(a, Pickup(a, Item(a.Margherita.Id, a.Small, 1, a.Garlic)));

        using var response = await b.Shop.GetAsync(new Uri($"/api/shop/orders/{placed.TrackingToken}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InvalidPhone_Returns400()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var request = Pickup(shop, Item(shop.Margherita.Id, shop.Small, 1, shop.Garlic)) with
        {
            Customer = new CustomerInput("Jan", "abc"),
        };

        using var response = await shop.Shop.PostAsJsonAsync("/api/shop/orders", request, TestShop.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static OrderItemInput Item(Guid productId, Guid sizeId, int quantity, params Guid[] modifiers) =>
        new(productId, null, sizeId, quantity, modifiers, null);

    private static PlaceOrderRequest Delivery(TestShop shop, params OrderItemInput[] items) =>
        new(FulfillmentType.Delivery, null, new DeliveryAddressInput(shop.StreetInZone, "15-001", "Białystok", "domofon 5"),
            new CustomerInput("Jan Kowalski", "600 100 200"), PaymentMethod.Cash, null, items);

    private static PlaceOrderRequest Pickup(TestShop shop, params OrderItemInput[] items) =>
        new(FulfillmentType.Pickup, shop.Location.Id, null, new CustomerInput("Anna", "+48600100200"), PaymentMethod.CardOnDelivery, null, items);

    private static async Task<PlacedOrderDto> PlaceAsync(TestShop shop, PlaceOrderRequest request)
    {
        using var response = await shop.Shop.PostAsJsonAsync("/api/shop/orders", request, TestShop.Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<PlacedOrderDto>(TestShop.Json))!;
    }
}
