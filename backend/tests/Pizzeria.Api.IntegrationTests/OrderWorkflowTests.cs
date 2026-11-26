using System.Net;
using System.Net.Http.Json;
using Pizzeria.Api.IntegrationTests.Infrastructure;
using Pizzeria.Application.Orders;
using Pizzeria.Domain.Identity;
using Pizzeria.Domain.Orders;

namespace Pizzeria.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class OrderWorkflowTests(PizzeriaApiFactory factory)
{
    private static int _nextArea;
    private readonly double _lat = 30 + Interlocked.Increment(ref _nextArea) * 0.5;

    [Fact]
    public async Task Staff_SeesActiveOrdersOfOwnLocation_AndOthersDoNot()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var placed = await PlaceDeliveryAsync(shop);
        using var staff = await LoginAsAsync(shop, Roles.Staff);
        var otherLocation = await shop.Tenant.Admin.CreateLocationAsync("Inny");
        using var stranger = await factory.LoginAsync(await shop.Tenant.Admin.CreateUserAsync(Roles.Staff, otherLocation.Id));

        var queue = await staff.GetFromJsonAsync<List<StaffOrderDto>>($"/api/locations/{shop.Location.Id}/orders", TestShop.Json);
        using var foreignList = await stranger.GetAsync(new Uri($"/api/locations/{shop.Location.Id}/orders", UriKind.Relative));
        using var foreignOrder = await stranger.GetAsync(new Uri($"/api/orders/{placed.Id}", UriKind.Relative));

        var order = Assert.Single(queue!);
        Assert.Equal((placed.Id, OrderStatus.New, OrderStatus.Accepted), (order.Id, order.Status, order.NextStatus));
        Assert.Equal("Jan Kowalski", order.CustomerName);
        Assert.Equal(HttpStatusCode.NotFound, foreignList.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignOrder.StatusCode);
    }

    [Fact]
    public async Task DeliveryFlow_StaffKitchenAndDriver_EachDoTheirPart()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var placed = await PlaceDeliveryAsync(shop);
        using var staff = await LoginAsAsync(shop, Roles.Staff);
        using var kitchen = await LoginAsAsync(shop, Roles.Kitchen);
        var (driverId, driver) = await DriverAsync(shop);

        await AdvanceAsync(staff, placed.Id, OrderStatus.Accepted);
        await AdvanceAsync(kitchen, placed.Id, OrderStatus.InPreparation);
        await AdvanceAsync(kitchen, placed.Id, OrderStatus.Ready);
        using var assign = await staff.PostAsJsonAsync($"/api/orders/{placed.Id}/driver", new AssignDriverRequest(driverId), TestShop.Json);
        var driverQueue = await driver.GetFromJsonAsync<List<StaffOrderDto>>($"/api/locations/{shop.Location.Id}/orders", TestShop.Json);
        await AdvanceAsync(driver, placed.Id, OrderStatus.InDelivery);
        var delivered = await AdvanceAsync(driver, placed.Id, OrderStatus.Delivered);
        var tracking = await shop.Shop.GetFromJsonAsync<OrderTrackingDto>($"/api/shop/orders/{placed.TrackingToken}", TestShop.Json);

        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        Assert.Equal([placed.Id], driverQueue!.Select(o => o.Id));
        Assert.Equal(PaymentStatus.Paid, delivered.PaymentStatus);
        Assert.Equal(OrderStatus.Delivered, tracking!.Status);
        Assert.Equal(
            [OrderStatus.New, OrderStatus.Accepted, OrderStatus.InPreparation, OrderStatus.Ready,
             OrderStatus.AssignedToDriver, OrderStatus.InDelivery, OrderStatus.Delivered],
            factory.Notifications!.For(placed.Id));
    }

    [Fact]
    public async Task Kitchen_CannotAcceptOrCancel()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var placed = await PlaceDeliveryAsync(shop);
        using var kitchen = await LoginAsAsync(shop, Roles.Kitchen);

        using var accept = await kitchen.PostAsync(new Uri($"/api/orders/{placed.Id}/advance", UriKind.Relative), null);
        using var cancel = await kitchen.PostAsJsonAsync($"/api/orders/{placed.Id}/cancel", new CancelOrderRequest("nie"), TestShop.Json);

        Assert.Equal(HttpStatusCode.Forbidden, accept.StatusCode);
        Assert.Equal("order.status_change_not_allowed", await AuthTests.ReadCodeAsync(accept));
        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
    }

    [Fact]
    public async Task Driver_CannotTouchSomeoneElsesDelivery()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var placed = await PlaceDeliveryAsync(shop);
        using var staff = await LoginAsAsync(shop, Roles.Staff);
        var (driverId, _) = await DriverAsync(shop);
        var (_, otherDriver) = await DriverAsync(shop);
        await AdvanceAsync(staff, placed.Id, OrderStatus.Accepted);
        await AdvanceAsync(staff, placed.Id, OrderStatus.InPreparation);
        await AdvanceAsync(staff, placed.Id, OrderStatus.Ready);
        using var assign = await staff.PostAsJsonAsync($"/api/orders/{placed.Id}/driver", new AssignDriverRequest(driverId), TestShop.Json);

        using var response = await otherDriver.PostAsync(new Uri($"/api/orders/{placed.Id}/advance", UriKind.Relative), null);
        var queue = await otherDriver.GetFromJsonAsync<List<StaffOrderDto>>($"/api/locations/{shop.Location.Id}/orders", TestShop.Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(queue!);
    }

    [Fact]
    public async Task ReadyDelivery_NeedsDriverNotNextStatus_AndOnlyDriversCanBeAssigned()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var placed = await PlaceDeliveryAsync(shop);
        using var staff = await LoginAsAsync(shop, Roles.Staff);
        var kitchenUser = await shop.Tenant.Admin.CreateUserAsync(Roles.Kitchen, shop.Location.Id);
        await AdvanceAsync(staff, placed.Id, OrderStatus.Accepted);
        await AdvanceAsync(staff, placed.Id, OrderStatus.InPreparation);
        await AdvanceAsync(staff, placed.Id, OrderStatus.Ready);
        var drivers = await staff.GetFromJsonAsync<List<DriverDto>>($"/api/locations/{shop.Location.Id}/drivers", TestShop.Json);
        var notADriver = (await shop.Tenant.Admin.GetFromJsonAsync<List<Application.Users.UserDto>>("/api/users"))!.Single(u => u.Email == kitchenUser).Id;

        using var advance = await staff.PostAsync(new Uri($"/api/orders/{placed.Id}/advance", UriKind.Relative), null);
        using var assign = await staff.PostAsJsonAsync($"/api/orders/{placed.Id}/driver", new AssignDriverRequest(notADriver), TestShop.Json);

        Assert.Empty(drivers!);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, advance.StatusCode);
        Assert.Equal("order.no_next_status", await AuthTests.ReadCodeAsync(advance));
        Assert.Equal(HttpStatusCode.NotFound, assign.StatusCode);
        Assert.Equal("order.driver_not_found", await AuthTests.ReadCodeAsync(assign));
    }

    [Fact]
    public async Task Cancel_NeedsReason_AndRemovesOrderFromActiveQueue()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var placed = await PlaceDeliveryAsync(shop);
        using var staff = await LoginAsAsync(shop, Roles.Staff);

        using var withoutReason = await staff.PostAsJsonAsync($"/api/orders/{placed.Id}/cancel", new CancelOrderRequest(" "), TestShop.Json);
        using var cancelled = await staff.PostAsJsonAsync($"/api/orders/{placed.Id}/cancel", new CancelOrderRequest("Brak ciasta"), TestShop.Json);
        var active = await staff.GetFromJsonAsync<List<StaffOrderDto>>($"/api/locations/{shop.Location.Id}/orders", TestShop.Json);
        var today = await staff.GetFromJsonAsync<List<StaffOrderDto>>($"/api/locations/{shop.Location.Id}/orders?scope=Today", TestShop.Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, withoutReason.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Empty(active!);
        Assert.Equal("Brak ciasta", Assert.Single(today!).CancellationReason);
    }

    [Fact]
    public async Task PhoneOrder_IsPlacedByStaffWithPhoneSource()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        using var staff = await LoginAsAsync(shop, Roles.Staff);
        using var kitchen = await LoginAsAsync(shop, Roles.Kitchen);
        var request = new PlaceOrderRequest(FulfillmentType.Pickup, shop.Location.Id, null, new CustomerInput("Pan z telefonu", "600700800"),
            PaymentMethod.Cash, "szybko", [new OrderItemInput(shop.Margherita.Id, null, shop.Small, 1, [shop.Garlic], "bez oregano")]);

        using var response = await staff.PostAsJsonAsync($"/api/locations/{shop.Location.Id}/orders", request, TestShop.Json);
        using var byKitchen = await kitchen.PostAsJsonAsync($"/api/locations/{shop.Location.Id}/orders", request, TestShop.Json);
        var placed = (await response.Content.ReadFromJsonAsync<PlacedOrderDto>(TestShop.Json))!;
        var order = await staff.GetFromJsonAsync<StaffOrderDto>($"/api/orders/{placed.Id}", TestShop.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, byKitchen.StatusCode);
        Assert.Equal((OrderSource.Phone, "szybko", "bez oregano"), (order!.Source, order.Notes, order.Items[0].Notes));
    }

    private static async Task<StaffOrderDto> AdvanceAsync(HttpClient client, Guid orderId, OrderStatus expected)
    {
        using var response = await client.PostAsync(new Uri($"/api/orders/{orderId}/advance", UriKind.Relative), null);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var order = (await response.Content.ReadFromJsonAsync<StaffOrderDto>(TestShop.Json))!;
        Assert.Equal(expected, order.Status);
        return order;
    }

    private Task<HttpClient> LoginAsAsync(TestShop shop, string role) =>
        shop.Tenant.Admin.CreateUserAsync(role, shop.Location.Id).ContinueWith(t => factory.LoginAsync(t.Result)).Unwrap();

    private async Task<(Guid Id, HttpClient Client)> DriverAsync(TestShop shop)
    {
        var email = await shop.Tenant.Admin.CreateUserAsync(Roles.Driver, shop.Location.Id);
        var users = await shop.Tenant.Admin.GetFromJsonAsync<List<Application.Users.UserDto>>("/api/users");
        return (users!.Single(u => u.Email == email).Id, await factory.LoginAsync(email));
    }

    private static async Task<PlacedOrderDto> PlaceDeliveryAsync(TestShop shop)
    {
        var request = new PlaceOrderRequest(FulfillmentType.Delivery, null,
            new DeliveryAddressInput(shop.StreetInZone, "15-001", "Białystok", null), new CustomerInput("Jan Kowalski", "600100200"),
            PaymentMethod.Cash, null, [new OrderItemInput(shop.Margherita.Id, null, shop.Large, 2, [shop.Garlic], null)]);
        using var response = await shop.Shop.PostAsJsonAsync("/api/shop/orders", request, TestShop.Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PlacedOrderDto>(TestShop.Json))!;
    }
}
