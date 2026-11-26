using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Pizzeria.Api.IntegrationTests.Infrastructure;
using Pizzeria.Application.Orders;
using Pizzeria.Application.Printing;
using Pizzeria.Domain.Identity;
using Pizzeria.Domain.Orders;
using Pizzeria.Domain.Printing;

namespace Pizzeria.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class PrintingTests(PizzeriaApiFactory factory)
{
    private static int _nextArea;
    private readonly double _lat = -30 - Interlocked.Increment(ref _nextArea) * 0.5;

    [Fact]
    public async Task AcceptedOrder_ReachesConnectedAgent_AndPrintedJobShowsInPanel()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var registered = await RegisterAgentAsync(shop.Tenant.Admin, shop.Location.Id);
        await using var agent = await ConnectAgentAsync(registered.Token);
        var wakeUp = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        agent.On("JobsAvailable", () => wakeUp.TrySetResult());
        await agent.InvokeAsync("Heartbeat", true, (bool?)null);

        var order = await PlaceAndAcceptAsync(shop);
        await wakeUp.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var jobs = await agent.InvokeAsync<List<AgentJobDto>>("FetchJobs");
        var again = await agent.InvokeAsync<List<AgentJobDto>>("FetchJobs");
        await agent.InvokeAsync("Confirm", jobs[0].Id, true, (string?)null);

        var job = Assert.Single(jobs);
        Assert.Equal(PrintJobKind.KitchenTicket, job.Kind);
        Assert.Contains("Margherita", job.Payload, StringComparison.Ordinal);
        Assert.Equal(order.Number, JsonDocument.Parse(job.Payload).RootElement.GetProperty("number").GetInt32());
        Assert.Empty(again); // sent jobs are not handed out twice
        var listed = Assert.Single(await GetAsync<List<PrintJobDto>>(shop.Tenant.Admin, $"/api/locations/{shop.Location.Id}/print-jobs"));
        Assert.Equal((order.Id, PrintJobStatus.Printed, 1), (listed.OrderId, listed.Status, listed.Attempts));
        var agentStatus = Assert.Single(await GetAsync<List<PrintAgentDto>>(shop.Tenant.Admin, $"/api/locations/{shop.Location.Id}/print-agents"));
        Assert.Equal((true, (bool?)true), (agentStatus.IsOnline, agentStatus.ReceiptPrinterOnline));
    }

    [Fact]
    public async Task WithoutAgent_NoJobsPileUp()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);

        await PlaceAndAcceptAsync(shop);

        Assert.Empty(await GetAsync<List<PrintJobDto>>(shop.Tenant.Admin, $"/api/locations/{shop.Location.Id}/print-jobs"));
    }

    [Fact]
    public async Task FailedJob_CanBeRetried_AndIsDeliveredAgain()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var registered = await RegisterAgentAsync(shop.Tenant.Admin, shop.Location.Id);
        await using var agent = await ConnectAgentAsync(registered.Token);
        using var staff = await factory.LoginAsync(await shop.Tenant.Admin.CreateUserAsync(Roles.Staff, shop.Location.Id));
        await PlaceAndAcceptAsync(shop);

        var first = Assert.Single(await agent.InvokeAsync<List<AgentJobDto>>("FetchJobs"));
        await agent.InvokeAsync("Confirm", first.Id, false, "Brak papieru");
        var failed = Assert.Single(await GetAsync<List<PrintJobDto>>(staff, $"/api/locations/{shop.Location.Id}/print-jobs"));
        using var retry = await staff.PostAsync(new Uri($"/api/print-jobs/{first.Id}/retry", UriKind.Relative), null);
        var second = Assert.Single(await agent.InvokeAsync<List<AgentJobDto>>("FetchJobs"));

        Assert.Equal((PrintJobStatus.Failed, "Brak papieru"), (failed.Status, failed.Error));
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task FiscalReceipt_FollowsLocationSetting()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        await RegisterAgentAsync(shop.Tenant.Admin, shop.Location.Id);
        using var settings = await shop.Tenant.Admin.PutAsJsonAsync(
            $"/api/locations/{shop.Location.Id}/print-settings",
            new PrintSettingsDto(false, FiscalReceiptTrigger.OnAccepted), TestShop.Json);

        await PlaceAndAcceptAsync(shop);

        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);
        var job = Assert.Single(await GetAsync<List<PrintJobDto>>(shop.Tenant.Admin, $"/api/locations/{shop.Location.Id}/print-jobs"));
        Assert.Equal(PrintJobKind.FiscalReceipt, job.Kind);
    }

    [Fact]
    public async Task AgentToken_OpensOnlyTheAgentHub_AndStaffTokensDoNotOpenIt()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var registered = await RegisterAgentAsync(shop.Tenant.Admin, shop.Location.Id);
        using var asAgent = factory.CreateClient();
        asAgent.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registered.Token);
        var staffToken = shop.Tenant.Admin.DefaultRequestHeaders.Authorization!.Parameter!;

        using var api = await asAgent.GetAsync(new Uri($"/api/locations/{shop.Location.Id}/print-jobs", UriKind.Relative));
        var staffOnHub = await Assert.ThrowsAsync<HttpRequestException>(() => ConnectAgentAsync(staffToken));

        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, staffOnHub.StatusCode);
    }

    [Fact]
    public async Task RevokedAgent_IsCutOff()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var registered = await RegisterAgentAsync(shop.Tenant.Admin, shop.Location.Id);
        await using var agent = await ConnectAgentAsync(registered.Token);

        using var revoke = await shop.Tenant.Admin.PostAsync(new Uri($"/api/print-agents/{registered.Agent.Id}/revoke", UriKind.Relative), null);
        // Long polling re-authenticates every request (401); a WebSocket connection is aborted by the hub.
        await Assert.ThrowsAnyAsync<Exception>(() => agent.InvokeAsync<List<AgentJobDto>>("FetchJobs"));
        var reconnect = await Assert.ThrowsAsync<HttpRequestException>(() => ConnectAgentAsync(registered.Token));

        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, reconnect.StatusCode);
    }

    [Fact]
    public async Task OtherCompany_CannotSeeOrManagePrinting()
    {
        var shop = await TestShop.CreateAsync(factory, _lat);
        var registered = await RegisterAgentAsync(shop.Tenant.Admin, shop.Location.Id);
        var other = await factory.CreateCompanyAsync("other");
        using var kitchen = await factory.LoginAsync(await shop.Tenant.Admin.CreateUserAsync(Roles.Kitchen, shop.Location.Id));

        using var list = await other.Admin.GetAsync(new Uri($"/api/locations/{shop.Location.Id}/print-agents", UriKind.Relative));
        using var register = await other.Admin.PostAsJsonAsync($"/api/locations/{shop.Location.Id}/print-agents", new RegisterAgentRequest("Obcy"));
        using var revoke = await other.Admin.PostAsync(new Uri($"/api/print-agents/{registered.Agent.Id}/revoke", UriKind.Relative), null);
        using var kitchenList = await kitchen.GetAsync(new Uri($"/api/locations/{shop.Location.Id}/print-jobs", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, register.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, revoke.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, kitchenList.StatusCode);
    }

    private static async Task<RegisteredAgentDto> RegisterAgentAsync(HttpClient manager, Guid locationId)
    {
        using var response = await manager.PostAsJsonAsync($"/api/locations/{locationId}/print-agents", new RegisterAgentRequest("Komputer w kuchni"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RegisteredAgentDto>(TestShop.Json))!;
    }

    private async Task<HubConnection> ConnectAgentAsync(string token)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/agent"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();
        try
        {
            await connection.StartAsync();
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        return connection;
    }

    private async Task<StaffOrderDto> PlaceAndAcceptAsync(TestShop shop)
    {
        var request = new PlaceOrderRequest(FulfillmentType.Pickup, shop.Location.Id, null, new CustomerInput("Jan Kowalski", "600100200"),
            PaymentMethod.Cash, null, [new OrderItemInput(shop.Margherita.Id, null, shop.Large, 1, [shop.Garlic], "Bez oregano")]);
        using var placed = await shop.Shop.PostAsJsonAsync("/api/shop/orders", request, TestShop.Json);
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        var order = (await placed.Content.ReadFromJsonAsync<PlacedOrderDto>(TestShop.Json))!;

        using var staff = await factory.LoginAsync(await shop.Tenant.Admin.CreateUserAsync(Roles.Staff, shop.Location.Id));
        using var accept = await staff.PostAsync(new Uri($"/api/orders/{order.Id}/advance", UriKind.Relative), null);
        Assert.True(accept.IsSuccessStatusCode, await accept.Content.ReadAsStringAsync());
        return (await accept.Content.ReadFromJsonAsync<StaffOrderDto>(TestShop.Json))!;
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string url) =>
        (await client.GetFromJsonAsync<T>(url, TestShop.Json))!;
}
