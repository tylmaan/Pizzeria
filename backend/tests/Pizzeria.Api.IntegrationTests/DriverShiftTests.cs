using System.Net;
using System.Net.Http.Json;
using Pizzeria.Api.IntegrationTests.Infrastructure;
using Pizzeria.Application.Drivers;
using Pizzeria.Application.Orders;
using Pizzeria.Domain.Drivers;
using Pizzeria.Domain.Identity;
using Pizzeria.Domain.Orders;

namespace Pizzeria.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class DriverShiftTests(PizzeriaApiFactory factory)
{
    private static int _nextArea;
    private readonly double _lat = 20 + Interlocked.Increment(ref _nextArea) * 0.5;

    [Fact]
    public async Task Shift_TotalsDeliveredOrdersAndIsSettledByManager()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var (driverId, driver) = await DriverAsync(shop);
        using var staff = await LoginAsAsync(shop, Roles.Staff);
        using var manager = await LoginAsAsync(shop, Roles.LocationManager);

        var started = await StartAsync(driver, shop, 100m);
        await DeliverAsync(shop, staff, driver, driverId, PaymentMethod.Cash);          // 74 zł
        await DeliverAsync(shop, staff, driver, driverId, PaymentMethod.CardOnDelivery); // 74 zł
        using var close = await driver.PostAsJsonAsync("/api/driver/shifts/current/close", new CloseShiftRequest(8m), TestShop.Json);
        var closed = (await close.Content.ReadFromJsonAsync<DriverShiftDto>(TestShop.Json))!;
        var waiting = await manager.GetFromJsonAsync<List<DriverShiftDto>>($"/api/locations/{shop.Location.Id}/shifts?status=Closed", TestShop.Json);
        using var noComment = await manager.PostAsJsonAsync($"/api/shifts/{closed.Id}/settle", new SettleShiftRequest(170m, null), TestShop.Json);
        using var settle = await manager.PostAsJsonAsync($"/api/shifts/{closed.Id}/settle", new SettleShiftRequest(170m, "Zgubione 4 zł"), TestShop.Json);
        var settled = (await settle.Content.ReadFromJsonAsync<DriverShiftDto>(TestShop.Json))!;

        Assert.Equal(DriverShiftStatus.Open, started.Status);
        Assert.Equal((2, 74m, 74m, 8m, 174m), (closed.DeliveriesCount, closed.CashCollected, closed.CardCollected, closed.Tips, closed.ExpectedCash));
        Assert.Equal([closed.Id], waiting!.Select(s => s.Id));
        Assert.NotNull(waiting![0].DriverName);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noComment.StatusCode);
        Assert.Equal("shift.discrepancy_comment_required", await AuthTests.ReadCodeAsync(noComment));
        Assert.Equal((DriverShiftStatus.Settled, -4m), (settled.Status, settled.Discrepancy!.Value));
    }

    [Fact]
    public async Task Driver_CannotOpenTwoShifts()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var (_, driver) = await DriverAsync(shop);
        await StartAsync(driver, shop, 50m);

        using var second = await driver.PostAsJsonAsync("/api/driver/shifts", new StartShiftRequest(shop.Location.Id, 50m), TestShop.Json);
        var current = await driver.GetFromJsonAsync<CurrentShiftDto>("/api/driver/shifts/current", TestShop.Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
        Assert.Equal("shift.already_open", await AuthTests.ReadCodeAsync(second));
        Assert.NotNull(current!.Shift);
    }

    [Fact]
    public async Task StaffAndOtherCompanies_CannotSettle()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var other = await TestShop.CreateAsync(factory, _lat + 0.1);
        var (_, driver) = await DriverAsync(shop);
        await StartAsync(driver, shop, 0m);
        using var close = await driver.PostAsJsonAsync("/api/driver/shifts/current/close", new CloseShiftRequest(0m), TestShop.Json);
        var closed = (await close.Content.ReadFromJsonAsync<DriverShiftDto>(TestShop.Json))!;
        using var staff = await LoginAsAsync(shop, Roles.Staff);

        using var byStaff = await staff.PostAsJsonAsync($"/api/shifts/{closed.Id}/settle", new SettleShiftRequest(0m, null), TestShop.Json);
        using var byOtherCompany = await other.Tenant.Admin.PostAsJsonAsync($"/api/shifts/{closed.Id}/settle", new SettleShiftRequest(0m, null), TestShop.Json);

        Assert.Equal(HttpStatusCode.Forbidden, byStaff.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, byOtherCompany.StatusCode);
    }

    private static async Task<DriverShiftDto> StartAsync(HttpClient driver, TestShop shop, decimal cash)
    {
        using var response = await driver.PostAsJsonAsync("/api/driver/shifts", new StartShiftRequest(shop.Location.Id, cash), TestShop.Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DriverShiftDto>(TestShop.Json))!;
    }

    private static async Task DeliverAsync(TestShop shop, HttpClient staff, HttpClient driver, Guid driverId, PaymentMethod payment)
    {
        var request = new PlaceOrderRequest(FulfillmentType.Delivery, null, new DeliveryAddressInput(shop.StreetInZone, "15-001", "Białystok", null),
            new CustomerInput("Jan", "600100200"), payment, null, [new OrderItemInput(shop.Margherita.Id, null, shop.Large, 2, [shop.Garlic], null)]);
        using var placed = await shop.Shop.PostAsJsonAsync("/api/shop/orders", request, TestShop.Json);
        var id = (await placed.Content.ReadFromJsonAsync<PlacedOrderDto>(TestShop.Json))!.Id;

        foreach (var client in new[] { staff, staff, staff })
        {
            using var r = await client.PostAsync(new Uri($"/api/orders/{id}/advance", UriKind.Relative), null);
            r.EnsureSuccessStatusCode();
        }

        using var assign = await staff.PostAsJsonAsync($"/api/orders/{id}/driver", new AssignDriverRequest(driverId), TestShop.Json);
        assign.EnsureSuccessStatusCode();
        foreach (var _ in new[] { 1, 2 })
        {
            using var r = await driver.PostAsync(new Uri($"/api/orders/{id}/advance", UriKind.Relative), null);
            r.EnsureSuccessStatusCode();
        }
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
