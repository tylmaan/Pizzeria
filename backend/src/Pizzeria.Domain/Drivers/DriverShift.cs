using Pizzeria.Domain.Common;
using Pizzeria.Domain.Menu;
using Pizzeria.Domain.Orders;

namespace Pizzeria.Domain.Drivers;

public enum DriverShiftStatus
{
    /// <summary>Driver is working.</summary>
    Open,

    /// <summary>Driver finished; the money is waiting to be counted by the manager.</summary>
    Closed,

    /// <summary>Manager counted the cash and confirmed the settlement.</summary>
    Settled,
}

public static class DriverShiftErrors
{
    public const string AlreadyOpen = "shift.already_open";
    public const string NotOpen = "shift.not_open";
    public const string NotClosed = "shift.not_closed";
    public const string CommentRequired = "shift.discrepancy_comment_required";
    public const string InvalidAmount = "shift.invalid_amount";
}

/// <summary>A delivered order counted into a shift.</summary>
public sealed record ShiftDelivery(PaymentMethod PaymentMethod, decimal Total);

/// <summary>
/// A driver's working shift and its cash settlement:
/// start with change money ("pogotówka") → deliver → close (totals per payment method, tips) →
/// the manager counts the cash and settles; any difference must be explained.
/// </summary>
public sealed class DriverShift : ILocationEntity
{
    public const int CommentMaxLength = 500;

    private DriverShift()
    {
    }

    public Guid Id { get; private set; }

    public Guid CompanyId { get; private set; }

    public Guid LocationId { get; private set; }

    public Guid DriverId { get; private set; }

    public DriverShiftStatus Status { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    /// <summary>Change money the driver took at the start.</summary>
    public decimal StartingCash { get; private set; }

    public int DeliveriesCount { get; private set; }

    /// <summary>Cash taken from customers (orders paid in cash on delivery).</summary>
    public decimal CashCollected { get; private set; }

    /// <summary>Paid by card at the door (already on the terminal, nothing to hand over).</summary>
    public decimal CardCollected { get; private set; }

    /// <summary>Paid online in advance.</summary>
    public decimal PaidOnline { get; private set; }

    /// <summary>Tips declared by the driver (the driver keeps them).</summary>
    public decimal Tips { get; private set; }

    /// <summary>Cash the driver has to hand over: change money plus cash collected.</summary>
    public decimal ExpectedCash => StartingCash + CashCollected;

    public decimal? CountedCash { get; private set; }

    /// <summary>Counted minus expected: negative = missing money.</summary>
    public decimal? Discrepancy => CountedCash - ExpectedCash;

    public string? DiscrepancyComment { get; private set; }

    public Guid? SettledByUserId { get; private set; }

    public DateTimeOffset? SettledAt { get; private set; }

    /// <param name="hasOpenShift">Whether the driver already has an open shift (one at a time).</param>
    public static DriverShift Start(Guid companyId, Guid locationId, Guid driverId, decimal startingCash, bool hasOpenShift, DateTimeOffset now)
    {
        if (hasOpenShift)
        {
            throw new DomainException(DriverShiftErrors.AlreadyOpen);
        }

        return new DriverShift
        {
            Id = Guid.CreateVersion7(now),
            CompanyId = companyId,
            LocationId = locationId,
            DriverId = driverId,
            Status = DriverShiftStatus.Open,
            StartedAt = now,
            StartingCash = RequireAmount(startingCash),
        };
    }

    /// <param name="deliveries">Orders the driver delivered during the shift.</param>
    /// <param name="tips">Tips declared by the driver.</param>
    /// <param name="now">End of the shift.</param>
    public void Close(IReadOnlyCollection<ShiftDelivery> deliveries, decimal tips, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(deliveries);

        if (Status != DriverShiftStatus.Open)
        {
            throw new DomainException(DriverShiftErrors.NotOpen);
        }

        DeliveriesCount = deliveries.Count;
        CashCollected = Sum(deliveries, PaymentMethod.Cash);
        CardCollected = Sum(deliveries, PaymentMethod.CardOnDelivery);
        PaidOnline = Sum(deliveries, PaymentMethod.Online);
        Tips = RequireAmount(tips);
        EndedAt = now;
        Status = DriverShiftStatus.Closed;
    }

    /// <summary>The manager confirms how much cash was handed over. A difference needs a comment.</summary>
    public void Settle(decimal countedCash, string? comment, Guid managerId, DateTimeOffset now)
    {
        if (Status != DriverShiftStatus.Closed)
        {
            throw new DomainException(DriverShiftErrors.NotClosed);
        }

        var trimmed = comment?.Trim();
        if (RequireAmount(countedCash) != ExpectedCash && string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException(DriverShiftErrors.CommentRequired);
        }

        CountedCash = countedCash;
        DiscrepancyComment = string.IsNullOrEmpty(trimmed) ? null : trimmed[..Math.Min(trimmed.Length, CommentMaxLength)];
        SettledByUserId = managerId;
        SettledAt = now;
        Status = DriverShiftStatus.Settled;
    }

    private static decimal Sum(IEnumerable<ShiftDelivery> deliveries, PaymentMethod method) =>
        deliveries.Where(d => d.PaymentMethod == method).Sum(d => d.Total);

    private static decimal RequireAmount(decimal amount)
    {
        try
        {
            return Money.RequirePrice(amount);
        }
        catch (DomainException)
        {
            throw new DomainException(DriverShiftErrors.InvalidAmount);
        }
    }
}
