using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Pizzeria.Api.Endpoints;
using Pizzeria.Api.IntegrationTests.Infrastructure;
using Pizzeria.Application.Orders;
using Pizzeria.Application.Payments;
using Pizzeria.Domain.Identity;
using Pizzeria.Domain.Orders;
using Pizzeria.Infrastructure.Payments;

namespace Pizzeria.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class PaymentTests(PizzeriaApiFactory factory)
{
    private static int _nextArea;
    private readonly double _lat = 0 + Interlocked.Increment(ref _nextArea) * 0.5;

    [Fact]
    public async Task OnlineOrder_RedirectsToPayment_AndCannotBeAcceptedBeforePaid()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        using var staff = await StaffAsync(shop);

        var placed = await PlaceOnlineAsync(shop);
        var order = await staff.GetFromJsonAsync<StaffOrderDto>($"/api/orders/{placed.Id}", TestShop.Json);
        using var accept = await staff.PostAsync(new Uri($"/api/orders/{placed.Id}/advance", UriKind.Relative), null);

        Assert.StartsWith("/pay/fake/fake_", placed.PaymentRedirectUrl, StringComparison.Ordinal);
        Assert.Contains($"return=%2Forder%2F{placed.TrackingToken}", placed.PaymentRedirectUrl, StringComparison.Ordinal);
        Assert.Equal((PaymentStatus.PendingOnline, (OrderStatus?)null), (order!.PaymentStatus, order.NextStatus));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, accept.StatusCode);
    }

    [Fact]
    public async Task SuccessfulPayment_MarksOrderPaid_RepeatedWebhooksChangeNothing()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        using var staff = await StaffAsync(shop);
        var placed = await PlaceOnlineAsync(shop);
        var providerId = ProviderId(placed);

        using var paid = await CompleteAsync(shop, providerId, succeeded: true);
        using var again = await CompleteAsync(shop, providerId, succeeded: true);
        using var lateFailure = await CompleteAsync(shop, providerId, succeeded: false);
        var tracking = await shop.Shop.GetFromJsonAsync<OrderTrackingDto>($"/api/shop/orders/{placed.TrackingToken}", TestShop.Json);
        using var accept = await staff.PostAsync(new Uri($"/api/orders/{placed.Id}/advance", UriKind.Relative), null);

        Assert.All(new[] { paid, again, lateFailure }, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(PaymentStatus.Paid, tracking!.PaymentStatus);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        Assert.Contains(OrderStatus.New, factory.Notifications!.For(placed.Id).Skip(1)); // "paid" pushed to the screens
    }

    [Fact]
    public async Task Webhook_WithBadSignature_IsRejected_ValidOneIsAccepted()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var placed = await PlaceOnlineAsync(shop);
        var body = FakePaymentProvider.Body(ProviderId(placed), succeeded: true);
        var fake = factory.Services.GetRequiredService<FakePaymentProvider>();

        using var forged = await PostWebhookAsync(body, "0000");
        using var genuine = await PostWebhookAsync(body, fake.Sign(body));
        var tracking = await shop.Shop.GetFromJsonAsync<OrderTrackingDto>($"/api/shop/orders/{placed.TrackingToken}", TestShop.Json);

        Assert.Equal(HttpStatusCode.Forbidden, forged.StatusCode);
        Assert.Equal("payment.invalid_signature", await AuthTests.ReadCodeAsync(forged));
        Assert.Equal(HttpStatusCode.OK, genuine.StatusCode);
        Assert.Equal(PaymentStatus.Paid, tracking!.PaymentStatus);
    }

    [Fact]
    public async Task FailedPayment_KeepsOrderUnpaid_AndCustomerCanRetry()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var placed = await PlaceOnlineAsync(shop);

        using var failed = await CompleteAsync(shop, ProviderId(placed), succeeded: false);
        using var retry = await shop.Shop.PostAsync(new Uri($"/api/shop/orders/{placed.TrackingToken}/payment", UriKind.Relative), null);
        var redirect = (await retry.Content.ReadFromJsonAsync<PaymentRedirectDto>(TestShop.Json))!;
        using var paid = await CompleteAsync(shop, redirect.RedirectUrl.Split('/', '?')[3], succeeded: true);
        var tracking = await shop.Shop.GetFromJsonAsync<OrderTrackingDto>($"/api/shop/orders/{placed.TrackingToken}", TestShop.Json);

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.NotEqual(placed.PaymentRedirectUrl, redirect.RedirectUrl);
        Assert.Equal(PaymentStatus.Paid, tracking!.PaymentStatus);
    }

    [Fact]
    public async Task PhoneOrder_CannotBePaidOnline()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        using var staff = await StaffAsync(shop);

        using var response = await staff.PostAsJsonAsync($"/api/locations/{shop.Location.Id}/orders", Request(shop), TestShop.Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("order.payment_method_not_available", await AuthTests.ReadCodeAsync(response));
    }

    private static string ProviderId(PlacedOrderDto placed) => placed.PaymentRedirectUrl!.Split('/', '?')[3];

    private static PlaceOrderRequest Request(TestShop shop) =>
        new(FulfillmentType.Pickup, shop.Location.Id, null, new CustomerInput("Jan", "600100200"), PaymentMethod.Online, null,
            [new OrderItemInput(shop.Margherita.Id, null, shop.Small, 1, [shop.Garlic], null)]);

    private static async Task<PlacedOrderDto> PlaceOnlineAsync(TestShop shop)
    {
        using var response = await shop.Shop.PostAsJsonAsync("/api/shop/orders", Request(shop), TestShop.Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PlacedOrderDto>(TestShop.Json))!;
    }

    private static Task<HttpResponseMessage> CompleteAsync(TestShop shop, string providerId, bool succeeded) =>
        shop.Shop.PostAsJsonAsync($"/api/payments/fake/{providerId}/complete", new FakePaymentCompletion(succeeded), TestShop.Json);

    private async Task<HttpResponseMessage> PostWebhookAsync(string body, string signature)
    {
        using var client = factory.CreateClient();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        content.Headers.Add(FakePaymentProvider.SignatureHeader, signature);
        return await client.PostAsync(new Uri("/api/payments/webhooks/fake", UriKind.Relative), content);
    }

    private async Task<HttpClient> StaffAsync(TestShop shop) =>
        await factory.LoginAsync(await shop.Tenant.Admin.CreateUserAsync(Roles.Staff, shop.Location.Id));
}
