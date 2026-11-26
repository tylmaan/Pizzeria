using Pizzeria.Application.Delivery;
using Pizzeria.Application.Locations;
using Pizzeria.Domain.Orders;

namespace Pizzeria.Application.Orders;

/// <summary>An order as staff, kitchen and drivers see it.</summary>
public sealed record StaffOrderDto(
    Guid Id,
    Guid LocationId,
    int Number,
    OrderStatus Status,
    OrderStatus? NextStatus,
    FulfillmentType Fulfillment,
    OrderSource Source,
    DateTimeOffset PlacedAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset StatusChangedAt,
    string CustomerName,
    string CustomerPhone,
    AddressDto? DeliveryAddress,
    string? DeliveryNotes,
    GeoPointDto? DeliveryPoint,
    string? Notes,
    IReadOnlyList<OrderItemDto> Items,
    decimal ItemsTotal,
    decimal DeliveryFee,
    decimal Total,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    Guid? DriverId,
    string? CancellationReason)
{
    public static StaffOrderDto From(Order o) => new(
        o.Id,
        o.LocationId,
        o.Number,
        o.Status,
        o.NextStatus,
        o.Fulfillment,
        o.Source,
        o.PlacedAt,
        o.StatusHistory.Where(h => h.ToStatus == OrderStatus.Accepted).Select(h => (DateTimeOffset?)h.ChangedAt).FirstOrDefault(),
        o.StatusHistory.Max(h => h.ChangedAt),
        o.CustomerName,
        o.CustomerPhone,
        o.DeliveryAddress is { } a ? AddressDto.From(a) : null,
        o.DeliveryNotes,
        o.DeliveryPoint is { } p ? new GeoPointDto(p.Y, p.X) : null,
        o.Notes,
        OrderItemDto.FromAll(o),
        o.ItemsTotal,
        o.DeliveryFee,
        o.Total,
        o.PaymentMethod,
        o.PaymentStatus,
        o.DriverId,
        o.CancellationReason);
}

public enum OrderListScope
{
    /// <summary>Not finished yet (the live queue).</summary>
    Active,

    /// <summary>Everything placed on the current business day of the location.</summary>
    Today,
}

public sealed record CancelOrderRequest(string Reason);

public sealed record AssignDriverRequest(Guid DriverId);

public sealed record DriverDto(Guid Id, string DisplayName);
