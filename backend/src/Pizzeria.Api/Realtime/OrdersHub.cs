using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Pizzeria.Api.Tenancy;
using Pizzeria.Application.Abstractions;
using Pizzeria.Application.Orders;
using Pizzeria.Application.Tenancy;
using Pizzeria.Domain.Identity;
using Pizzeria.Domain.Orders;

namespace Pizzeria.Api.Realtime;

/// <summary>
/// Live order notifications. Groups: <c>location:{id}</c> (order queue, kitchen), <c>order:{id}</c>
/// (customer tracking), <c>driver:{userId}</c> (driver app). Messages only say "this order changed";
/// clients reload the current state from the REST API.
/// </summary>
public sealed class OrdersHub(ITenantContext tenant, IAppDbContext db) : Hub
{
    public const string Path = "/hubs/orders";
    public const string OrderChanged = nameof(OrderChanged);

    public static string LocationGroup(Guid locationId) => $"location:{locationId}";

    public static string OrderGroup(Guid orderId) => $"order:{orderId}";

    public static string DriverGroup(Guid userId) => $"driver:{userId}";

    public override async Task OnConnectedAsync()
    {
        if (tenant.IsInRole(Roles.Driver) && tenant.UserId is { } userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, DriverGroup(userId)).ConfigureAwait(false);
        }

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    /// <summary>Staff and kitchen screens of a location.</summary>
    public async Task JoinLocation(Guid locationId)
    {
        if (!tenant.CanAccessLocation(locationId))
        {
            throw new HubException("location.not_found");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, LocationGroup(locationId)).ConfigureAwait(false);
    }

    /// <summary>Customer tracking page (anonymous; the tenant comes from the shop host).</summary>
    public async Task TrackOrder(string token)
    {
        var orderId = await db.Orders.Where(o => o.TrackingToken == token).Select(o => (Guid?)o.Id).FirstOrDefaultAsync().ConfigureAwait(false)
            ?? throw new HubException("order.not_found");

        await Groups.AddToGroupAsync(Context.ConnectionId, OrderGroup(orderId)).ConfigureAwait(false);
    }
}

/// <summary>
/// Hub invocations do not go through the HTTP middleware pipeline, so the tenant is resolved here
/// for the connection event and every hub method call.
/// </summary>
public sealed class HubTenantFilter : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        ArgumentNullException.ThrowIfNull(invocationContext);
        ArgumentNullException.ThrowIfNull(next);

        await ResolveAsync(invocationContext.ServiceProvider, invocationContext.Context).ConfigureAwait(false);
        return await next(invocationContext).ConfigureAwait(false);
    }

    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        await ResolveAsync(context.ServiceProvider, context.Context).ConfigureAwait(false);
        await next(context).ConfigureAwait(false);
    }

    private static Task ResolveAsync(IServiceProvider services, HubCallerContext caller) =>
        services.GetRequiredService<TenantResolver>().ResolveAsync(
            caller.User ?? new System.Security.Claims.ClaimsPrincipal(),
            caller.GetHttpContext()?.Request.Host.Host ?? string.Empty,
            caller.ConnectionAborted);
}

public sealed class SignalROrderNotifier(IHubContext<OrdersHub> hub) : IOrderNotifier
{
    public Task OrderChangedAsync(Order order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);

        List<string> groups = [OrdersHub.LocationGroup(order.LocationId), OrdersHub.OrderGroup(order.Id)];
        if (order.DriverId is { } driverId)
        {
            groups.Add(OrdersHub.DriverGroup(driverId));
        }

        return hub.Clients.Groups(groups).SendAsync(
            OrdersHub.OrderChanged,
            new { orderId = order.Id, locationId = order.LocationId, number = order.Number, status = order.Status.ToString() },
            ct);
    }
}
