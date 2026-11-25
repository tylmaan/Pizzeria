namespace Pizzeria.Domain.Orders;

/// <summary>
/// <code>
/// New → Accepted → InPreparation → Ready ─┬─ (delivery) → AssignedToDriver → InDelivery → Delivered
///                                         └─ (pickup)   → PickedUp
/// Every state before Delivered/PickedUp → Cancelled (with a reason)
/// </code>
/// </summary>
public enum OrderStatus
{
    New,
    Accepted,
    InPreparation,
    Ready,
    AssignedToDriver,
    InDelivery,
    Delivered,
    PickedUp,
    Cancelled,
}

public enum FulfillmentType
{
    Delivery,
    Pickup,
}

public enum OrderSource
{
    Online,
    Phone,
    InStore,
}

/// <summary>Independent of <see cref="OrderStatus"/>: an order can be delivered and still unpaid.</summary>
public enum PaymentStatus
{
    Unpaid,
    PendingOnline,
    Paid,
    Refunded,
}

public enum PaymentMethod
{
    Cash,
    CardOnDelivery,
    Online,
}

public static class OrderErrors
{
    public const string InvalidStatusTransition = "order.invalid_status_transition";
    public const string ItemsRequired = "order.items_required";
    public const string InvalidQuantity = "order.invalid_quantity";
    public const string BelowMinimumValue = "order.below_minimum_value";
    public const string AddressRequired = "order.address_required";
    public const string CancellationReasonRequired = "order.cancellation_reason_required";
    public const string InvalidCustomer = "order.invalid_customer";
    public const string PaymentMethodNotAvailable = "order.payment_method_not_available";
}
