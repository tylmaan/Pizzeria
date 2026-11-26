using Pizzeria.Application.Locations;
using Pizzeria.Application.Menu;
using Pizzeria.Domain.Orders;

namespace Pizzeria.Application.Orders;

/// <summary>Public data of a company shop: name, locations for pickup and the orderable menu.</summary>
/// <param name="OnlinePayments">Whether the company accepts online payments (has a payment provider set up).</param>
public sealed record ShopDto(string CompanyName, IReadOnlyList<ShopLocationDto> Locations, MenuDto Menu, bool OnlinePayments);

public sealed record ShopLocationDto(Guid Id, string Name, AddressDto Address);

public sealed record CustomerInput(string Name, string Phone);

public sealed record DeliveryAddressInput(string Street, string PostalCode, string City, string? Notes);

public sealed record OrderItemInput(
    Guid ProductId,
    Guid? SecondHalfProductId,
    Guid SizeId,
    int Quantity,
    IReadOnlyList<Guid> ModifierIds,
    string? Notes);

/// <summary>
/// A new order. Contains no prices: the API prices every item from the current menu and ignores
/// anything the client might believe the price is.
/// </summary>
public sealed record PlaceOrderRequest(
    FulfillmentType Fulfillment,
    Guid? PickupLocationId,
    DeliveryAddressInput? DeliveryAddress,
    CustomerInput Customer,
    PaymentMethod PaymentMethod,
    string? Notes,
    IReadOnlyList<OrderItemInput> Items);

/// <param name="PaymentRedirectUrl">Where to send the customer to pay (online payment only).</param>
public sealed record PlacedOrderDto(Guid Id, int Number, string TrackingToken, decimal Total, string? PaymentRedirectUrl);

public sealed record OrderItemModifierDto(string Name, decimal Price);

public sealed record OrderItemDto(
    string ProductName,
    string? SecondHalfProductName,
    string SizeName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    IReadOnlyList<OrderItemModifierDto> Modifiers,
    string? Notes)
{
    public static OrderItemDto From(OrderItem i) => new(
        i.ProductName,
        i.SecondHalfProductName,
        i.SizeName,
        i.Quantity,
        i.UnitPrice,
        i.LineTotal,
        [.. i.Modifiers.Select(m => new OrderItemModifierDto(m.Name, m.Price))],
        i.Notes);

    public static IReadOnlyList<OrderItemDto> FromAll(Order o) => [.. o.Items.OrderBy(i => i.SortOrder).Select(From)];
}

public sealed record OrderStatusChangeDto(OrderStatus Status, DateTimeOffset At);

/// <summary>What the customer sees on the tracking page (no staff or internal data).</summary>
public sealed record OrderTrackingDto(
    int Number,
    OrderStatus Status,
    FulfillmentType Fulfillment,
    string LocationName,
    DateTimeOffset PlacedAt,
    IReadOnlyList<OrderItemDto> Items,
    decimal ItemsTotal,
    decimal DeliveryFee,
    decimal Total,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    AddressDto? DeliveryAddress,
    string? CancellationReason,
    IReadOnlyList<OrderStatusChangeDto> History);
