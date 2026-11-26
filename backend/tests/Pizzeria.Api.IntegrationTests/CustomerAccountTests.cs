using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Pizzeria.Api.IntegrationTests.Infrastructure;
using Pizzeria.Application.Customers;
using Pizzeria.Application.Orders;
using Pizzeria.Application.Users;
using Pizzeria.Domain.Orders;

namespace Pizzeria.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class CustomerAccountTests(PizzeriaApiFactory factory)
{
    private static int _nextArea;
    private readonly double _lat = 10 + Interlocked.Increment(ref _nextArea) * 0.5;

    [Fact]
    public async Task Customer_RegistersOrders_AndSeesOnlyOwnOrdersInHistory()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        using var customer = await RegisterAsync(shop, Email());

        var mine = await PlacePickupAsync(shop, customer);
        await PlacePickupAsync(shop, shop.Shop); // a guest order in the same shop
        var profile = await customer.GetFromJsonAsync<CustomerProfileDto>("/api/shop/account", TestShop.Json);
        var history = await customer.GetFromJsonAsync<List<CustomerOrderDto>>("/api/shop/account/orders", TestShop.Json);

        Assert.Equal(("Anna", "600100200"), (profile!.Name, profile.Phone));
        var order = Assert.Single(history!);
        Assert.Equal((mine.Id, mine.TrackingToken, OrderStatus.New), (order.Id, order.TrackingToken, order.Status));
        Assert.Equal(["1× Margherita 42 cm"], order.Items);
    }

    [Fact]
    public async Task Accounts_ArePerCompany_SameEmailIsSeparateAccount()
    {
        var first = await TestShop.CreateAsync(factory, _lat);
        var second = await TestShop.CreateAsync(factory, _lat);
        var email = Email();
        using var inFirst = await RegisterAsync(first, email);
        using var inSecond = await RegisterAsync(second, email, password: "inna-haslo-2");

        using var duplicate = await PostAsync(first.Shop, "/api/shop/account/register", new RegisterCustomerRequest(email, PizzeriaApiFactory.Password, "Anna", null));
        using var wrongShop = await PostAsync(first.Shop, "/api/shop/account/login", new CustomerLoginRequest(email, "inna-haslo-2"));
        using var rightShop = await PostAsync(second.Shop, "/api/shop/account/login", new CustomerLoginRequest(email, "inna-haslo-2"));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongShop.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rightShop.StatusCode);
    }

    [Fact]
    public async Task Customer_CannotUseStaffPanelOrStaffApi_AndIsNotListedAsStaff()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var email = Email();
        using var customer = await RegisterAsync(shop, email);
        using var anonymous = factory.CreateClient();

        using var staffLogin = await anonymous.PostAsJsonAsync("/api/auth/login", new { email, password = PizzeriaApiFactory.Password });
        using var prefixedLogin = await anonymous.PostAsJsonAsync("/api/auth/login",
            new { email = CustomerUserNames.For(shop.Tenant.Company.Id, email), password = PizzeriaApiFactory.Password });
        using var locations = await customer.GetAsync(new Uri("/api/locations", UriKind.Relative));
        using var orders = await customer.GetAsync(new Uri($"/api/locations/{shop.Location.Id}/orders", UriKind.Relative));
        var staff = await shop.Tenant.Admin.GetFromJsonAsync<List<UserDto>>("/api/users", TestShop.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, staffLogin.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, prefixedLogin.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, locations.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, orders.StatusCode);
        Assert.DoesNotContain(staff!, u => u.Email == email);
    }

    [Fact]
    public async Task CustomerToken_InAnotherCompanysShop_CountsAsGuest()
    {
        var home = await TestShop.CreateAsync(factory, _lat);
        var other = await TestShop.CreateAsync(factory, _lat);
        using var customer = await RegisterAsync(home, Email());
        using var elsewhere = factory.CreateClient();
        elsewhere.DefaultRequestHeaders.Host = other.Shop.DefaultRequestHeaders.Host;
        elsewhere.DefaultRequestHeaders.Authorization = customer.DefaultRequestHeaders.Authorization;

        using var account = await elsewhere.GetAsync(new Uri("/api/shop/account", UriKind.Relative));
        await PlacePickupAsync(other, elsewhere);
        var history = await customer.GetFromJsonAsync<List<CustomerOrderDto>>("/api/shop/account/orders", TestShop.Json);

        Assert.Equal(HttpStatusCode.Forbidden, account.StatusCode);
        Assert.Empty(history!);
    }

    [Fact]
    public async Task SavedAddresses_AreOwnedByTheCustomer()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        using var customer = await RegisterAsync(shop, Email());
        using var someoneElse = await RegisterAsync(shop, Email());

        using var created = await PostAsync(customer, "/api/shop/account/addresses", new SaveAddressRequest("Dom", "ul. Lipowa 5", "15-001", "Białystok", "domofon 5"));
        var address = (await created.Content.ReadFromJsonAsync<SavedAddressDto>(TestShop.Json))!;
        using var invalid = await PostAsync(customer, "/api/shop/account/addresses", new SaveAddressRequest(null, "ul. Lipowa 5", "15001", "Białystok", null));
        using var foreignUpdate = await someoneElse.PutAsJsonAsync($"/api/shop/account/addresses/{address.Id}",
            new SaveAddressRequest("Moje", "ul. Inna 1", "15-001", "Białystok", null), TestShop.Json);
        using var foreignDelete = await someoneElse.DeleteAsync(new Uri($"/api/shop/account/addresses/{address.Id}", UriKind.Relative));
        var mine = await customer.GetFromJsonAsync<List<SavedAddressDto>>("/api/shop/account/addresses", TestShop.Json);
        var theirs = await someoneElse.GetFromJsonAsync<List<SavedAddressDto>>("/api/shop/account/addresses", TestShop.Json);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignDelete.StatusCode);
        Assert.Equal([("Dom", "ul. Lipowa 5", "domofon 5")], mine!.Select(a => (a.Label, a.Street, a.Notes)));
        Assert.Empty(theirs!);
    }

    [Fact]
    public async Task DeletedAccount_CannotSignIn_ButItsOrdersStay()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var email = Email();
        using var customer = await RegisterAsync(shop, email);
        var order = await PlacePickupAsync(shop, customer);
        await PostAsync(customer, "/api/shop/account/addresses", new SaveAddressRequest(null, "ul. Lipowa 5", "15-001", "Białystok", null));

        using var deleted = await customer.DeleteAsync(new Uri("/api/shop/account", UriKind.Relative));
        using var login = await PostAsync(shop.Shop, "/api/shop/account/login", new CustomerLoginRequest(email, PizzeriaApiFactory.Password));
        var tracking = await shop.Shop.GetFromJsonAsync<OrderTrackingDto>($"/api/shop/orders/{order.TrackingToken}", TestShop.Json);
        var queue = await shop.Tenant.Admin.GetFromJsonAsync<List<StaffOrderDto>>($"/api/locations/{shop.Location.Id}/orders", TestShop.Json);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal(order.Number, tracking!.Number);
        Assert.Contains(queue!, o => o.Id == order.Id);
    }

    [Fact]
    public async Task Registration_ValidatesInput()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);

        using var response = await PostAsync(shop.Shop, "/api/shop/account/register", new RegisterCustomerRequest("nie-email", "krotkie", "", "123"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"email\"", body, StringComparison.Ordinal);
        Assert.Contains("\"name\"", body, StringComparison.Ordinal);
        Assert.Contains("validation.phone", body, StringComparison.Ordinal);
    }

    private static string Email() => $"klient-{Guid.NewGuid():N}@test.local";

    private async Task<HttpClient> RegisterAsync(TestShop shop, string email, string password = PizzeriaApiFactory.Password)
    {
        using var response = await PostAsync(shop.Shop, "/api/shop/account/register", new RegisterCustomerRequest(email, password, "Anna", "600100200"));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var token = (await response.Content.ReadFromJsonAsync<AccessTokenResponse>())!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = shop.Shop.DefaultRequestHeaders.Host;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }

    private static async Task<PlacedOrderDto> PlacePickupAsync(TestShop shop, HttpClient client)
    {
        var request = new PlaceOrderRequest(FulfillmentType.Pickup, shop.Location.Id, null, new CustomerInput("Anna", "600100200"),
            PaymentMethod.Cash, null, [new OrderItemInput(shop.Margherita.Id, null, shop.Large, 1, [shop.Garlic], null)]);
        using var response = await PostAsync(client, "/api/shop/orders", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PlacedOrderDto>(TestShop.Json))!;
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string url, object body) =>
        client.PostAsJsonAsync(url, body, TestShop.Json);
}
