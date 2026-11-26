using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pizzeria.Application.Abstractions;
using Pizzeria.Application.Errors;
using Pizzeria.Application.Tenancy;
using Pizzeria.Application.Users;
using Pizzeria.Domain.Drivers;
using Pizzeria.Domain.Identity;
using Pizzeria.Domain.Orders;

namespace Pizzeria.Application.Drivers;

public sealed record DriverShiftDto(
    Guid Id,
    Guid LocationId,
    Guid DriverId,
    string? DriverName,
    DriverShiftStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    decimal StartingCash,
    int DeliveriesCount,
    decimal CashCollected,
    decimal CardCollected,
    decimal PaidOnline,
    decimal Tips,
    decimal ExpectedCash,
    decimal? CountedCash,
    decimal? Discrepancy,
    string? DiscrepancyComment,
    DateTimeOffset? SettledAt)
{
    public static DriverShiftDto From(DriverShift s, string? driverName = null) => new(
        s.Id, s.LocationId, s.DriverId, driverName, s.Status, s.StartedAt, s.EndedAt, s.StartingCash, s.DeliveriesCount,
        s.CashCollected, s.CardCollected, s.PaidOnline, s.Tips, s.ExpectedCash, s.CountedCash, s.Discrepancy,
        s.DiscrepancyComment, s.SettledAt);
}

/// <summary>The driver's current shift; <see cref="Shift"/> is null when the driver is off duty.</summary>
public sealed record CurrentShiftDto(DriverShiftDto? Shift);

public sealed record StartShiftRequest(Guid LocationId, decimal StartingCash);

public sealed record CloseShiftRequest(decimal Tips);

public sealed record SettleShiftRequest(decimal CountedCash, string? Comment);

public sealed class SettleShiftRequestValidator : AbstractValidator<SettleShiftRequest>
{
    public SettleShiftRequestValidator()
    {
        RuleFor(r => r.CountedCash).GreaterThanOrEqualTo(0m).PrecisionScale(10, 2, ignoreTrailingZeros: true).WithErrorCode("validation.price");
        RuleFor(r => r.Comment).MaximumLength(DriverShift.CommentMaxLength);
    }
}

/// <summary>Driver shifts: drivers start and close their own, managers settle the cash.</summary>
public sealed class DriverShiftService(
    IAppDbContext db,
    ITenantContext tenant,
    TimeProvider clock,
    IUserAccountService accounts,
    IValidator<SettleShiftRequest> settleValidator)
{
    public async Task<DriverShiftDto> StartAsync(StartShiftRequest request, CancellationToken ct)
    {
        var driverId = RequireDriver();
        if (!tenant.CanAccessLocation(request.LocationId))
        {
            throw new NotFoundException(ErrorCodes.LocationNotFound);
        }

        var hasOpen = await db.DriverShifts.AnyAsync(s => s.DriverId == driverId && s.Status == DriverShiftStatus.Open, ct).ConfigureAwait(false);
        var shift = DriverShift.Start(tenant.CompanyId!.Value, request.LocationId, driverId, request.StartingCash, hasOpen, clock.GetUtcNow());

        db.DriverShifts.Add(shift);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return DriverShiftDto.From(shift);
    }

    public async Task<CurrentShiftDto> GetCurrentAsync(CancellationToken ct)
    {
        var driverId = RequireDriver();
        var shift = await db.DriverShifts.AsNoTracking()
            .FirstOrDefaultAsync(s => s.DriverId == driverId && s.Status == DriverShiftStatus.Open, ct).ConfigureAwait(false);
        return new CurrentShiftDto(shift is null ? null : DriverShiftDto.From(shift));
    }

    /// <summary>Ends the shift and totals the orders the driver delivered since it started.</summary>
    public async Task<DriverShiftDto> CloseAsync(CloseShiftRequest request, CancellationToken ct)
    {
        var driverId = RequireDriver();
        var shift = await db.DriverShifts
            .FirstOrDefaultAsync(s => s.DriverId == driverId && s.Status == DriverShiftStatus.Open, ct).ConfigureAwait(false)
            ?? throw new NotFoundException(ErrorCodes.ShiftNotFound);

        var since = shift.StartedAt;
        var deliveries = await db.Orders.AsNoTracking()
            .Where(o => o.DriverId == driverId && o.Status == OrderStatus.Delivered
                        && o.StatusHistory.Any(h => h.ToStatus == OrderStatus.Delivered && h.ChangedAt >= since))
            .Select(o => new { o.PaymentMethod, o.Total })
            .ToListAsync(ct).ConfigureAwait(false);

        shift.Close([.. deliveries.Select(d => new ShiftDelivery(d.PaymentMethod, d.Total))], request.Tips, clock.GetUtcNow());
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return DriverShiftDto.From(shift);
    }

    /// <summary>Shifts of a location for the manager, newest first (by default those waiting for settlement).</summary>
    public async Task<IReadOnlyList<DriverShiftDto>> ListAsync(Guid locationId, DriverShiftStatus? status, CancellationToken ct)
    {
        RequireManagerOf(locationId);

        var query = db.DriverShifts.AsNoTracking().Where(s => s.LocationId == locationId);
        if (status is { } wanted)
        {
            query = query.Where(s => s.Status == wanted);
        }

        var shifts = await query.OrderByDescending(s => s.StartedAt).Take(100).ToListAsync(ct).ConfigureAwait(false);
        var names = (await accounts.ListAsync(tenant.CompanyId!.Value, [locationId], ct).ConfigureAwait(false))
            .ToDictionary(u => u.Id, u => u.DisplayName);
        return shifts.ConvertAll(s => DriverShiftDto.From(s, names.GetValueOrDefault(s.DriverId)));
    }

    public async Task<DriverShiftDto> SettleAsync(Guid id, SettleShiftRequest request, CancellationToken ct)
    {
        await settleValidator.ValidateAndThrowAsync(request, ct).ConfigureAwait(false);
        var shift = await db.DriverShifts.FirstOrDefaultAsync(s => s.Id == id, ct).ConfigureAwait(false)
            ?? throw new NotFoundException(ErrorCodes.ShiftNotFound);
        RequireManagerOf(shift.LocationId);

        shift.Settle(request.CountedCash, request.Comment, tenant.UserId!.Value, clock.GetUtcNow());
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return DriverShiftDto.From(shift);
    }

    private Guid RequireDriver() =>
        tenant.IsInRole(Roles.Driver) && tenant.UserId is { } id ? id : throw new ForbiddenException();

    private void RequireManagerOf(Guid locationId)
    {
        var isManager = tenant.IsCompanyAdmin || tenant.IsInRole(Roles.LocationManager);
        if (!isManager || !tenant.CanAccessLocation(locationId))
        {
            throw new NotFoundException(ErrorCodes.LocationNotFound);
        }
    }
}
