using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pizzeria.Api.Background;
using Pizzeria.Api.IntegrationTests.Infrastructure;
using Pizzeria.Application.Drivers;
using Pizzeria.Application.Orders;
using Pizzeria.Application.Tenancy;
using Pizzeria.Domain.Delivery;
using Pizzeria.Domain.Drivers;
using Pizzeria.Domain.Identity;
using Pizzeria.Domain.Orders;
using Pizzeria.Infrastructure.Persistence;

namespace Pizzeria.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class DriverTrackingTests(PizzeriaApiFactory factory)
{
    private static int _nextArea;
    private readonly double _lat = 10 + Interlocked.Increment(ref _nextArea) * 0.5;

    [Fact]
    public async Task Position_WithoutOpenShift_IsRejected()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var (_, driver) = await DriverAsync(shop);

        using var response = await Report(driver, _lat, 20);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("shift.not_open", await AuthTests.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Staff_SeesLatestPositionOfDriversOnShift_KitchenDoesNot()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var (driverId, driver) = await DriverAsync(shop);
        await StartShiftAsync(driver, shop);
        using var staff = await LoginAsAsync(shop, Roles.Staff);
        using var kitchen = await LoginAsAsync(shop, Roles.Kitchen);

        (await Report(driver, _lat, 20)).Dispose();
        (await Report(driver, _lat + 0.001, 20.002)).Dispose();
        var positions = await staff.GetFromJsonAsync<List<DriverPositionDto>>($"/api/locations/{shop.Location.Id}/drivers/positions", TestShop.Json);
        using var byKitchen = await kitchen.GetAsync(new Uri($"/api/locations/{shop.Location.Id}/drivers/positions", UriKind.Relative));

        var position = Assert.Single(positions!);
        Assert.Equal((driverId, _lat + 0.001, 20.002), (position.DriverId, position.Latitude, position.Longitude));
        Assert.NotNull(position.DriverName);
        Assert.Equal(HttpStatusCode.NotFound, byKitchen.StatusCode);
    }

    [Fact]
    public async Task Customer_SeesDriverOnlyWhileTheirOrderIsOnTheWay()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var (driverId, driver) = await DriverAsync(shop);
        await StartShiftAsync(driver, shop);
        using var staff = await LoginAsAsync(shop, Roles.Staff);
        var order = await PlaceAndAssignAsync(shop, staff, driverId);
        (await Report(driver, _lat - 0.01, 20)).Dispose(); // before setting off: must stay private

        var beforeSettingOff = await shop.Shop.GetFromJsonAsync<OrderDriverPositionDto>($"/api/shop/orders/{order.TrackingToken}/driver", TestShop.Json);
        (await driver.PostAsync(new Uri($"/api/orders/{order.Id}/advance", UriKind.Relative), null)).Dispose();
        var justSetOff = await shop.Shop.GetFromJsonAsync<OrderDriverPositionDto>($"/api/shop/orders/{order.TrackingToken}/driver", TestShop.Json);
        (await Report(driver, _lat + 0.005, 20)).Dispose();
        var onTheWay = await shop.Shop.GetFromJsonAsync<OrderDriverPositionDto>($"/api/shop/orders/{order.TrackingToken}/driver", TestShop.Json);

        Assert.Null(beforeSettingOff!.Position);
        Assert.Null(justSetOff!.Position);
        Assert.Equal(_lat + 0.005, onTheWay!.Position!.Latitude);
        Assert.Null(onTheWay.Position.DriverName);
    }

    [Fact]
    public async Task Retention_DeletesTrailOlderThan30Days()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var (driverId, driver) = await DriverAsync(shop);
        await StartShiftAsync(driver, shop);
        (await Report(driver, _lat, 20)).Dispose();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().SetSystemForCompany(shop.Tenant.Company.Id);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var shift = await db.DriverShifts.SingleAsync(s => s.DriverId == driverId);
            db.DriverPositions.Add(DriverPosition.Record(shift, new GeoPoint(_lat, 20), null, DateTimeOffset.UtcNow.AddDays(-31)));
            await db.SaveChangesAsync();
        }

        await factory.Services.GetRequiredService<DriverPositionRetention>().DeleteExpiredAsync(CancellationToken.None);

        await using var check = factory.Services.CreateAsyncScope();
        var left = await check.ServiceProvider.GetRequiredService<AppDbContext>().DriverPositions
            .IgnoreQueryFilters().Where(p => p.DriverId == driverId).CountAsync();
        Assert.Equal(1, left);
    }

    private static Task<HttpResponseMessage> Report(HttpClient driver, double lat, double lon) =>
        driver.PostAsJsonAsync("/api/driver/position", new ReportPositionRequest(lat, lon, 12), TestShop.Json);

    private static async Task StartShiftAsync(HttpClient driver, TestShop shop)
    {
        using var response = await driver.PostAsJsonAsync("/api/driver/shifts", new StartShiftRequest(shop.Location.Id, 0m), TestShop.Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<PlacedOrderDto> PlaceAndAssignAsync(TestShop shop, HttpClient staff, Guid driverId)
    {
        var request = new PlaceOrderRequest(FulfillmentType.Delivery, null, new DeliveryAddressInput(shop.StreetInZone, "15-001", "Białystok", null),
            new CustomerInput("Jan", "600100200"), PaymentMethod.Cash, null, [new OrderItemInput(shop.Margherita.Id, null, shop.Large, 2, [shop.Garlic], null)]);
        using var placed = await shop.Shop.PostAsJsonAsync("/api/shop/orders", request, TestShop.Json);
        var order = (await placed.Content.ReadFromJsonAsync<PlacedOrderDto>(TestShop.Json))!;
        for (var i = 0; i < 3; i++)
        {
            (await staff.PostAsync(new Uri($"/api/orders/{order.Id}/advance", UriKind.Relative), null)).Dispose();
        }

        (await staff.PostAsJsonAsync($"/api/orders/{order.Id}/driver", new AssignDriverRequest(driverId), TestShop.Json)).Dispose();
        return order;
    }

    private async Task<HttpClient> LoginAsAsync(TestShop shop, string role) =>
        await factory.LoginAsync(await shop.Tenant.Admin.CreateUserAsync(role, shop.Location.Id));

    private async Task<(Guid Id, HttpClient Client)> DriverAsync(TestShop shop)
    {
        var email = await shop.Tenant.Admin.CreateUserAsync(Roles.Driver, shop.Location.Id);
        var users = await shop.Tenant.Admin.GetFromJsonAsync<List<Application.Users.UserDto>>("/api/users");
        return (users!.Single(u => u.Email == email).Id, await factory.LoginAsync(email));
    }
}
