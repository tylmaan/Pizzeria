using System.Security.Cryptography;
using NetTopologySuite.Geometries;
using Pizzeria.Domain.Common;
using Pizzeria.Domain.Delivery;
using Pizzeria.Domain.Tenancy;
using Location = Pizzeria.Domain.Tenancy.Location;

namespace Pizzeria.Domain.Orders;

/// <summary>One entry of the status history: who changed what, when and why.</summary>
public sealed class OrderStatusChange
{
    private OrderStatusChange()
    {
    }

    internal OrderStatusChange(OrderStatus? from, OrderStatus to, DateTimeOffset at, Guid? byUserId, string? reason)
    {
        Id = Guid.CreateVersion7(at);
        FromStatus = from;
        ToStatus = to;
        ChangedAt = at;
        ChangedByUserId = byUserId;
        Reason = reason;
    }

    public Guid Id { get; private set; }

    public OrderStatus? FromStatus { get; private set; }

    public OrderStatus ToStatus { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    /// <summary><c>null</c> for changes made by the customer (placing an online order) or the system.</summary>
    public Guid? ChangedByUserId { get; private set; }

    public string? Reason { get; private set; }
}

public sealed record OrderCustomer(string Name, string Phone);

/// <summary>Delivery terms resolved from the zone the address lies in.</summary>
public sealed record DeliveryTerms(Address Address, GeoPoint? Point, string? AddressNotes, decimal Fee, decimal MinOrderValue);

/// <summary>
/// A customer order. Status only changes through its methods, which enforce the state machine and
/// record every change in <see cref="StatusHistory"/>.
/// </summary>
public sealed class Order : ILocationEntity
{
    public const int CustomerNameMaxLength = 100;
    public const int PhoneMaxLength = 20;
    public const int NotesMaxLength = 500;
    public const int ReasonMaxLength = 300;

    private static readonly Dictionary<OrderStatus, OrderStatus[]> Transitions = new()
    {
        [OrderStatus.New] = [OrderStatus.Accepted, OrderStatus.Cancelled],
        [OrderStatus.Accepted] = [OrderStatus.InPreparation, OrderStatus.Cancelled],
        [OrderStatus.InPreparation] = [OrderStatus.Ready, OrderStatus.Cancelled],
        [OrderStatus.Ready] = [OrderStatus.AssignedToDriver, OrderStatus.PickedUp, OrderStatus.Cancelled],
        [OrderStatus.AssignedToDriver] = [OrderStatus.InDelivery, OrderStatus.Cancelled],
        [OrderStatus.InDelivery] = [OrderStatus.Delivered, OrderStatus.Cancelled],
        [OrderStatus.Delivered] = [],
        [OrderStatus.PickedUp] = [],
        [OrderStatus.Cancelled] = [],
    };

    private readonly List<OrderItem> _items = [];
    private readonly List<OrderStatusChange> _statusHistory = [];

    private Order()
    {
    }

    public Guid Id { get; private set; }

    public Guid CompanyId { get; private set; }

    public Guid LocationId { get; private set; }

    /// <summary>Short number shown to staff and the customer, unique per location and day (e.g. 127).</summary>
    public int Number { get; private set; }

    /// <summary>Secret in the customer's tracking link (works for guests, no account needed).</summary>
    public string TrackingToken { get; private set; } = null!;

    public OrderSource Source { get; private set; }

    public FulfillmentType Fulfillment { get; private set; }

    public OrderStatus Status { get; private set; }

    public PaymentMethod PaymentMethod { get; private set; }

    public PaymentStatus PaymentStatus { get; private set; }

    public string CustomerName { get; private set; } = null!;

    public string CustomerPhone { get; private set; } = null!;

    public Address? DeliveryAddress { get; private set; }

    public Point? DeliveryPoint { get; private set; }

    public string? DeliveryNotes { get; private set; }

    public string? Notes { get; private set; }

    public decimal ItemsTotal { get; private set; }

    public decimal DeliveryFee { get; private set; }

    public decimal Total { get; private set; }

    public Guid? DriverId { get; private set; }

    public string? CancellationReason { get; private set; }

    public DateTimeOffset PlacedAt { get; private set; }

    public IReadOnlyList<OrderItem> Items => _items;

    public IReadOnlyList<OrderStatusChange> StatusHistory => _statusHistory;

    /// <param name="delivery">Delivery terms, or <c>null</c> for pickup.</param>
    public static Order Place(
        Location location,
        int number,
        OrderSource source,
        OrderCustomer customer,
        DeliveryTerms? delivery,
        IReadOnlyList<OrderLine> lines,
        PaymentMethod paymentMethod,
        string? notes,
        Guid? placedByUserId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Count == 0)
        {
            throw new DomainException(OrderErrors.ItemsRequired);
        }

        var name = customer.Name?.Trim() ?? string.Empty;
        var phone = customer.Phone?.Trim() ?? string.Empty;
        if (name.Length is 0 or > CustomerNameMaxLength || phone.Length is 0 or > PhoneMaxLength)
        {
            throw new DomainException(OrderErrors.InvalidCustomer);
        }

        var order = new Order
        {
            Id = Guid.CreateVersion7(now),
            CompanyId = location.CompanyId,
            LocationId = location.Id,
            Number = number,
            TrackingToken = NewTrackingToken(),
            Source = source,
            Fulfillment = delivery is null ? FulfillmentType.Pickup : FulfillmentType.Delivery,
            Status = OrderStatus.New,
            PaymentMethod = paymentMethod,
            // Online orders wait for the payment provider's confirmation before the restaurant may accept them.
            PaymentStatus = paymentMethod == PaymentMethod.Online ? PaymentStatus.PendingOnline : PaymentStatus.Unpaid,
            CustomerName = name,
            CustomerPhone = phone,
            Notes = Trim(notes, NotesMaxLength),
            PlacedAt = now,
        };

        order._items.AddRange(lines.Select(OrderItem.From));
        order.ItemsTotal = order._items.Sum(i => i.LineTotal);

        if (delivery is not null)
        {
            if (order.ItemsTotal < delivery.MinOrderValue)
            {
                throw new DomainException(OrderErrors.BelowMinimumValue);
            }

            order.DeliveryAddress = delivery.Address;
            order.DeliveryPoint = delivery.Point?.ToPoint();
            order.DeliveryNotes = Trim(delivery.AddressNotes, NotesMaxLength);
            order.DeliveryFee = delivery.Fee;
        }

        order.Total = order.ItemsTotal + order.DeliveryFee;
        order._statusHistory.Add(new OrderStatusChange(null, OrderStatus.New, now, placedByUserId, null));
        return order;
    }

    public bool CanMoveTo(OrderStatus next) =>
        Transitions[Status].Contains(next)
        && next switch
        {
            OrderStatus.AssignedToDriver or OrderStatus.InDelivery or OrderStatus.Delivered => Fulfillment == FulfillmentType.Delivery,
            OrderStatus.PickedUp => Fulfillment == FulfillmentType.Pickup,
            _ => true,
        };

    /// <summary>
    /// The next status of the normal flow — what the one-tap button on the kitchen and order screens does.
    /// <c>null</c> when the next step needs more input (assigning a driver) or the order is finished.
    /// </summary>
    public OrderStatus? NextStatus => Status switch
    {
        OrderStatus.New when AwaitingPayment => null,
        OrderStatus.New => OrderStatus.Accepted,
        OrderStatus.Accepted => OrderStatus.InPreparation,
        OrderStatus.InPreparation => OrderStatus.Ready,
        OrderStatus.Ready when Fulfillment == FulfillmentType.Pickup => OrderStatus.PickedUp,
        OrderStatus.AssignedToDriver => OrderStatus.InDelivery,
        OrderStatus.InDelivery => OrderStatus.Delivered,
        _ => null,
    };

    /// <summary>An online order whose payment has not been confirmed yet.</summary>
    public bool AwaitingPayment => PaymentStatus == PaymentStatus.PendingOnline;

    public void Accept(Guid? byUserId, DateTimeOffset now)
    {
        if (AwaitingPayment)
        {
            throw new DomainException(OrderErrors.AwaitingPayment);
        }

        MoveTo(OrderStatus.Accepted, byUserId, now);
    }

    /// <summary>
    /// The payment provider confirmed the online payment. Idempotent: a repeated confirmation changes nothing.
    /// A cancelled order is still marked paid (the money arrived and has to be refunded).
    /// </summary>
    public void MarkPaidOnline(DateTimeOffset now)
    {
        if (PaymentMethod != PaymentMethod.Online)
        {
            throw new DomainException(OrderErrors.PaymentMethodNotAvailable);
        }

        if (PaymentStatus == PaymentStatus.Paid)
        {
            return;
        }

        PaymentStatus = PaymentStatus.Paid;
        _statusHistory.Add(new OrderStatusChange(Status, Status, now, null, "paid online"));
    }

    public void StartPreparation(Guid? byUserId, DateTimeOffset now) => MoveTo(OrderStatus.InPreparation, byUserId, now);

    public void MarkReady(Guid? byUserId, DateTimeOffset now) => MoveTo(OrderStatus.Ready, byUserId, now);

    public void MarkPickedUp(Guid? byUserId, DateTimeOffset now)
    {
        MoveTo(OrderStatus.PickedUp, byUserId, now);
        SettleOnHandover();
    }

    /// <summary>Assigns a driver to a ready delivery order, or switches the driver before the delivery starts.</summary>
    public void AssignDriver(Guid driverId, Guid? byUserId, DateTimeOffset now)
    {
        if (Status == OrderStatus.AssignedToDriver)
        {
            DriverId = driverId;
            _statusHistory.Add(new OrderStatusChange(Status, Status, now, byUserId, "driver changed"));
            return;
        }

        MoveTo(OrderStatus.AssignedToDriver, byUserId, now);
        DriverId = driverId;
    }

    /// <summary>The driver took the order from the restaurant ("Odebrałem z lokalu").</summary>
    public void StartDelivery(Guid? byUserId, DateTimeOffset now) => MoveTo(OrderStatus.InDelivery, byUserId, now);

    public void MarkDelivered(Guid? byUserId, DateTimeOffset now)
    {
        MoveTo(OrderStatus.Delivered, byUserId, now);
        SettleOnHandover();
    }

    public void Cancel(string reason, Guid? byUserId, DateTimeOffset now)
    {
        var trimmed = Trim(reason, ReasonMaxLength);
        if (trimmed is null)
        {
            throw new DomainException(OrderErrors.CancellationReasonRequired);
        }

        MoveTo(OrderStatus.Cancelled, byUserId, now, trimmed);
        CancellationReason = trimmed;
    }

    /// <summary>Applies <see cref="NextStatus"/>.</summary>
    public void Advance(Guid? byUserId, DateTimeOffset now)
    {
        switch (NextStatus)
        {
            case OrderStatus.Accepted: Accept(byUserId, now); break;
            case OrderStatus.InPreparation: StartPreparation(byUserId, now); break;
            case OrderStatus.Ready: MarkReady(byUserId, now); break;
            case OrderStatus.PickedUp: MarkPickedUp(byUserId, now); break;
            case OrderStatus.InDelivery: StartDelivery(byUserId, now); break;
            case OrderStatus.Delivered: MarkDelivered(byUserId, now); break;
            default: throw new DomainException(OrderErrors.InvalidStatusTransition);
        }
    }

    private void MoveTo(OrderStatus next, Guid? byUserId, DateTimeOffset now, string? reason = null)
    {
        if (!CanMoveTo(next))
        {
            throw new DomainException(OrderErrors.InvalidStatusTransition, $"Order {Id}: {Status} → {next} is not allowed.");
        }

        _statusHistory.Add(new OrderStatusChange(Status, next, now, byUserId, reason));
        Status = next;
    }

    // Cash or card on delivery is collected when the customer gets the food.
    private void SettleOnHandover()
    {
        if (PaymentMethod is PaymentMethod.Cash or PaymentMethod.CardOnDelivery)
        {
            PaymentStatus = PaymentStatus.Paid;
        }
    }

    private static string? Trim(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed[..Math.Min(trimmed.Length, maxLength)];
    }

    private static string NewTrackingToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).Replace('+', '-').Replace('/', '_');
}
