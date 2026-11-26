using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Pizzeria.Api.IntegrationTests.Infrastructure;
using Pizzeria.Application.Orders;
using Pizzeria.Application.Reports;
using Pizzeria.Domain.Identity;
using Pizzeria.Domain.Orders;

namespace Pizzeria.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class ReportTests(PizzeriaApiFactory factory)
{
    private static int _nextArea;
    private readonly double _lat = -10 - Interlocked.Increment(ref _nextArea) * 0.5;

    /// <summary>Today as the business day of a location in Europe/Warsaw.</summary>
    private static string Today =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw")).DateTime)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Fact]
    public async Task Report_CountsCompletedOrdersPerPaymentMethod_BestSellersTimesAndDrivers()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var other = await TestShop.CreateAsync(factory, _lat - 0.2);
        using var staff = await LoginAsAsync(shop, Roles.Staff);
        var (driverId, driver) = await DriverAsync(shop);

        await DeliverAsync(shop, staff, driver, driverId);                                 // cash, 2 × Margherita 42 cm = 68 + 6
        await PickUpAsync(shop, staff, PaymentMethod.CardOnDelivery);                       // card, 1 × Margherita 32 cm = 27
        var cancelled = await PlaceAsync(shop, Pickup(shop, PaymentMethod.Cash));
        (await staff.PostAsJsonAsync($"/api/orders/{cancelled.Id}/cancel", new CancelOrderRequest("test"), TestShop.Json)).Dispose();
        using var otherStaff = await LoginAsAsync(other, Roles.Staff);
        await PickUpAsync(other, otherStaff, PaymentMethod.Cash);                           // another company: must not count

        var report = await shop.Tenant.Admin.GetFromJsonAsync<ReportDto>(
            $"/api/reports?locationId={shop.Location.Id}&from={Today}&to={Today}", TestShop.Json);

        Assert.Equal(new ReportSummaryDto(2, 1, 101m, 50.5m), report!.Summary);
        Assert.Equal(
            [("CardOnDelivery", 1, 27m, 0m), ("Cash", 1, 74m, 6m)],
            report.Revenue.Select(r => (r.PaymentMethod, r.Orders, r.Total, r.DeliveryFees)));
        Assert.Equal(new BestSellerDto("Margherita", 3, 95m), Assert.Single(report.BestSellers));
        Assert.NotNull(report.Times.PlacedToCompleted);
        Assert.NotNull(report.Times.Delivery);
        var driverRow = Assert.Single(report.Drivers);
        Assert.Equal((driverId, 1, 74m), (driverRow.DriverId, driverRow.Deliveries, driverRow.Total));
        Assert.NotNull(driverRow.DriverName);
    }

    [Fact]
    public async Task CompanyWideReport_IsForCompanyAdminsOnly()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var second = await shop.Tenant.Admin.CreateLocationAsync("Drugi");
        using var manager = await LoginAsAsync(shop, Roles.LocationManager);
        using var staff = await LoginAsAsync(shop, Roles.Staff);

        using var companyWide = await shop.Tenant.Admin.GetAsync(new Uri($"/api/reports?from={Today}&to={Today}", UriKind.Relative));
        using var ownLocation = await manager.GetAsync(new Uri($"/api/reports?locationId={shop.Location.Id}&from={Today}&to={Today}", UriKind.Relative));
        using var managerCompanyWide = await manager.GetAsync(new Uri($"/api/reports?from={Today}&to={Today}", UriKind.Relative));
        using var managerOtherLocation = await manager.GetAsync(new Uri($"/api/reports?locationId={second.Id}&from={Today}&to={Today}", UriKind.Relative));
        using var byStaff = await staff.GetAsync(new Uri($"/api/reports?locationId={shop.Location.Id}&from={Today}&to={Today}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, companyWide.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownLocation.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, managerCompanyWide.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, managerOtherLocation.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byStaff.StatusCode);
    }

    [Fact]
    public async Task InvalidRange_Returns422()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);

        using var reversed = await shop.Tenant.Admin.GetAsync(new Uri("/api/reports?from=2026-11-20&to=2026-11-10", UriKind.Relative));
        using var tooLong = await shop.Tenant.Admin.GetAsync(new Uri("/api/reports?from=2025-01-01&to=2026-11-10", UriKind.Relative));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, reversed.StatusCode);
        Assert.Equal("report.invalid_range", await AuthTests.ReadCodeAsync(reversed));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooLong.StatusCode);
    }

    private static PlaceOrderRequest Pickup(TestShop shop, PaymentMethod payment) =>
        new(FulfillmentType.Pickup, shop.Location.Id, null, new CustomerInput("Anna", "600100200"), payment, null,
            [new OrderItemInput(shop.Margherita.Id, null, shop.Small, 1, [shop.Garlic], null)]);

    private static async Task<PlacedOrderDto> PlaceAsync(TestShop shop, PlaceOrderRequest request)
    {
        using var response = await shop.Shop.PostAsJsonAsync("/api/shop/orders", request, TestShop.Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PlacedOrderDto>(TestShop.Json))!;
    }

    private static async Task AdvanceAsync(HttpClient client, Guid orderId, int times)
    {
        for (var i = 0; i < times; i++)
        {
            using var response = await client.PostAsync(new Uri($"/api/orders/{orderId}/advance", UriKind.Relative), null);
            response.EnsureSuccessStatusCode();
        }
    }

    private static async Task PickUpAsync(TestShop shop, HttpClient staff, PaymentMethod payment)
    {
        var placed = await PlaceAsync(shop, Pickup(shop, payment));
        await AdvanceAsync(staff, placed.Id, 4);
    }

    private static async Task DeliverAsync(TestShop shop, HttpClient staff, HttpClient driver, Guid driverId)
    {
        var placed = await PlaceAsync(shop, new PlaceOrderRequest(FulfillmentType.Delivery, null,
            new DeliveryAddressInput(shop.StreetInZone, "15-001", "Białystok", null), new CustomerInput("Jan", "600100200"),
            PaymentMethod.Cash, null, [new OrderItemInput(shop.Margherita.Id, null, shop.Large, 2, [shop.Garlic], null)]));
        await AdvanceAsync(staff, placed.Id, 3);
        (await staff.PostAsJsonAsync($"/api/orders/{placed.Id}/driver", new AssignDriverRequest(driverId), TestShop.Json)).EnsureSuccessStatusCode();
        await AdvanceAsync(driver, placed.Id, 2);
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
