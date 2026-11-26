using Microsoft.EntityFrameworkCore;
using Pizzeria.Application.Abstractions;
using Pizzeria.Application.Errors;
using Pizzeria.Application.Tenancy;
using Pizzeria.Application.Users;
using Pizzeria.Domain.Delivery;
using Pizzeria.Domain.Drivers;
using Pizzeria.Domain.Identity;
using Pizzeria.Domain.Orders;

namespace Pizzeria.Application.Drivers;

public sealed record ReportPositionRequest(double Latitude, double Longitude, double? AccuracyMeters);

public sealed record DriverPositionDto(Guid DriverId, string? DriverName, double Latitude, double Longitude, DateTimeOffset RecordedAt);

/// <summary>Where the driver of a tracked order is; <see cref="Position"/> is null unless the order is on its way.</summary>
public sealed record OrderDriverPositionDto(DriverPositionDto? Position);

/// <summary>Pushes driver positions to the location's screens and to customers tracking an order of that driver.</summary>
public interface IDriverPositionNotifier
{
    Task PositionChangedAsync(DriverPosition position, IReadOnlyList<Guid> ordersOnTheWay, CancellationToken ct);
}

public sealed class NullDriverPositionNotifier : IDriverPositionNotifier
{
    public Task PositionChangedAsync(DriverPosition position, IReadOnlyList<Guid> ordersOnTheWay, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>Live driver positions. Only during an open shift; customers see a driver only for their own order on its way.</summary>
public sealed class DriverTrackingService(
    IAppDbContext db,
    ITenantContext tenant,
    TimeProvider clock,
    IUserAccountService accounts,
    IDriverPositionNotifier notifier)
{
    /// <summary>Positions older than this are not shown on the live map.</summary>
    public static readonly TimeSpan LiveWindow = TimeSpan.FromMinutes(15);

    public async Task RecordAsync(ReportPositionRequest request, CancellationToken ct)
    {
        if (!tenant.IsInRole(Roles.Driver) || tenant.UserId is not { } driverId)
        {
            throw new ForbiddenException();
        }

        var shift = await db.DriverShifts
            .FirstOrDefaultAsync(s => s.DriverId == driverId && s.Status == DriverShiftStatus.Open, ct).ConfigureAwait(false)
            ?? throw new UnprocessableException(DriverShiftErrors.NotOpen);

        var position = DriverPosition.Record(shift, new GeoPoint(request.Latitude, request.Longitude), request.AccuracyMeters, clock.GetUtcNow());
        db.DriverPositions.Add(position);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var onTheWay = await db.Orders
            .Where(o => o.DriverId == driverId && o.Status == OrderStatus.InDelivery)
            .Select(o => o.Id)
            .ToListAsync(ct).ConfigureAwait(false);
        await notifier.PositionChangedAsync(position, onTheWay, ct).ConfigureAwait(false);
    }

    /// <summary>Latest position of every driver on shift at the location (for the live map in the panel).</summary>
    public async Task<IReadOnlyList<DriverPositionDto>> ListLatestAsync(Guid locationId, CancellationToken ct)
    {
        var mayWatch = tenant.IsCompanyAdmin || tenant.IsInRole(Roles.LocationManager) || tenant.IsInRole(Roles.Staff);
        if (!mayWatch || !tenant.CanAccessLocation(locationId))
        {
            throw new NotFoundException(ErrorCodes.LocationNotFound);
        }

        var since = clock.GetUtcNow() - LiveWindow;
        var latest = await db.DriverPositions.AsNoTracking()
            .Where(p => p.LocationId == locationId && p.RecordedAt >= since
                        && db.DriverShifts.Any(s => s.Id == p.ShiftId && s.Status == DriverShiftStatus.Open))
            .GroupBy(p => p.DriverId)
            .Select(g => g.OrderByDescending(p => p.RecordedAt).First())
            .ToListAsync(ct).ConfigureAwait(false);

        var names = (await accounts.ListAsync(tenant.CompanyId!.Value, [locationId], ct).ConfigureAwait(false))
            .ToDictionary(u => u.Id, u => u.DisplayName);
        return latest.ConvertAll(p => ToDto(p, names.GetValueOrDefault(p.DriverId)));
    }

    /// <summary>Customer tracking: the driver's last position, only while this order is on its way.</summary>
    public async Task<OrderDriverPositionDto> GetForOrderAsync(string token, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking()
            .Where(o => o.TrackingToken == token)
            .Select(o => new { o.Status, o.DriverId, OnTheWaySince = o.StatusHistory.Where(h => h.ToStatus == OrderStatus.InDelivery).Max(h => (DateTimeOffset?)h.ChangedAt) })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false)
            ?? throw new NotFoundException(ErrorCodes.OrderNotFound);

        if (order.Status != OrderStatus.InDelivery || order.DriverId is not { } driverId || order.OnTheWaySince is not { } since)
        {
            return new OrderDriverPositionDto(null);
        }

        // Positions from before the driver set off would reveal where else the driver was.
        var position = await db.DriverPositions.AsNoTracking()
            .Where(p => p.DriverId == driverId && p.RecordedAt >= since)
            .OrderByDescending(p => p.RecordedAt)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return new OrderDriverPositionDto(position is null ? null : ToDto(position, null));
    }

    private static DriverPositionDto ToDto(DriverPosition p, string? name) =>
        new(p.DriverId, name, p.Position.Latitude, p.Position.Longitude, p.RecordedAt);
}
