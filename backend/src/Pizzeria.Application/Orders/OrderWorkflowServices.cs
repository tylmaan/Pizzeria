using Microsoft.EntityFrameworkCore;
using Pizzeria.Application.Abstractions;
using Pizzeria.Application.Errors;
using Pizzeria.Application.Tenancy;
using Pizzeria.Application.Users;
using Pizzeria.Domain.Identity;
using Pizzeria.Domain.Orders;

namespace Pizzeria.Application.Orders;

/// <summary>Orders of a location for the order queue, kitchen and driver screens.</summary>
public sealed class OrderQueueService(IAppDbContext db, ITenantContext tenant, TimeProvider clock)
{
    private static readonly OrderStatus[] Finished = [OrderStatus.Delivered, OrderStatus.PickedUp, OrderStatus.Cancelled];

    public async Task<IReadOnlyList<StaffOrderDto>> ListAsync(Guid locationId, OrderListScope scope, CancellationToken ct)
    {
        if (!tenant.CanAccessLocation(locationId))
        {
            throw new NotFoundException(ErrorCodes.LocationNotFound);
        }

        var location = await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId, ct).ConfigureAwait(false)
            ?? throw new NotFoundException(ErrorCodes.LocationNotFound);

        var query = db.Orders.AsNoTracking().Where(o => o.LocationId == locationId);
        if (scope == OrderListScope.Active)
        {
            query = query.Where(o => !Finished.Contains(o.Status));
        }
        else
        {
            var since = StartOfBusinessDay(location.TimeZoneId, clock.GetUtcNow());
            query = query.Where(o => o.PlacedAt >= since);
        }

        // Drivers only see their own deliveries; kitchen and front of house see everything of the location.
        if (tenant.IsInRole(Roles.Driver) && !tenant.IsCompanyAdmin
            && !tenant.IsInRole(Roles.LocationManager) && !tenant.IsInRole(Roles.Staff) && !tenant.IsInRole(Roles.Kitchen))
        {
            var me = tenant.UserId;
            query = query.Where(o => o.DriverId == me);
        }

        var orders = await query.OrderBy(o => o.PlacedAt).ToListAsync(ct).ConfigureAwait(false);
        return orders.ConvertAll(StaffOrderDto.From);
    }

    public async Task<StaffOrderDto> GetAsync(Guid id, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct).ConfigureAwait(false);
        if (order is null || !OrderPermissions.CanView(tenant, order))
        {
            throw new NotFoundException(ErrorCodes.OrderNotFound);
        }

        return StaffOrderDto.From(order);
    }

    private static DateTimeOffset StartOfBusinessDay(string timeZoneId, DateTimeOffset now)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var local = TimeZoneInfo.ConvertTime(now, zone);
        var midnight = new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(midnight, zone.GetUtcOffset(midnight)).ToUniversalTime();
    }
}

/// <summary>Status changes from staff, kitchen and drivers. Every change is saved and broadcast.</summary>
public sealed class OrderWorkflowService(
    IAppDbContext db,
    ITenantContext tenant,
    TimeProvider clock,
    IUserAccountService accounts,
    IOrderNotifier notifier)
{
    /// <summary>"One tap = next status" (accept, start, ready, picked up, on the way, delivered).</summary>
    public Task<StaffOrderDto> AdvanceAsync(Guid id, CancellationToken ct) =>
        ChangeAsync(id, order => order.NextStatus ?? throw new UnprocessableException(ErrorCodes.NoNextStatus),
            order => order.Advance(tenant.UserId, clock.GetUtcNow()), ct);

    public Task<StaffOrderDto> CancelAsync(Guid id, CancelOrderRequest request, CancellationToken ct) =>
        ChangeAsync(id, _ => OrderStatus.Cancelled,
            order => order.Cancel(request.Reason, tenant.UserId, clock.GetUtcNow()), ct);

    public Task<StaffOrderDto> AssignDriverAsync(Guid id, AssignDriverRequest request, CancellationToken ct) =>
        ChangeAsync(id, _ => OrderStatus.AssignedToDriver, async order =>
        {
            if (!await accounts.IsDriverAtLocationAsync(request.DriverId, order.LocationId, ct).ConfigureAwait(false))
            {
                throw new NotFoundException(ErrorCodes.DriverNotFound);
            }

            order.AssignDriver(request.DriverId, tenant.UserId, clock.GetUtcNow());
        }, ct);

    /// <summary>Drivers of a location, for the "assign driver" picker.</summary>
    public async Task<IReadOnlyList<DriverDto>> ListDriversAsync(Guid locationId, CancellationToken ct)
    {
        if (!tenant.CanAccessLocation(locationId))
        {
            throw new NotFoundException(ErrorCodes.LocationNotFound);
        }

        var users = await accounts.ListAsync(tenant.CompanyId!.Value, [locationId], ct).ConfigureAwait(false);
        return [.. users.Where(u => u.Role == Roles.Driver).Select(u => new DriverDto(u.Id, u.DisplayName))];
    }

    private Task<StaffOrderDto> ChangeAsync(Guid id, Func<Order, OrderStatus> target, Action<Order> change, CancellationToken ct) =>
        ChangeAsync(id, target, order =>
        {
            change(order);
            return Task.CompletedTask;
        }, ct);

    private async Task<StaffOrderDto> ChangeAsync(Guid id, Func<Order, OrderStatus> target, Func<Order, Task> change, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct).ConfigureAwait(false);
        if (order is null || !OrderPermissions.CanView(tenant, order))
        {
            throw new NotFoundException(ErrorCodes.OrderNotFound);
        }

        if (!OrderPermissions.CanMoveTo(tenant, order, target(order)))
        {
            throw new ForbiddenException(ErrorCodes.StatusChangeNotAllowed);
        }

        await change(order).ConfigureAwait(false);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await notifier.OrderChangedAsync(order, ct).ConfigureAwait(false);
        return StaffOrderDto.From(order);
    }
}
