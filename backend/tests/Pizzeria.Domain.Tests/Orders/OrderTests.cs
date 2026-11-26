using Pizzeria.Domain.Common;
using Pizzeria.Domain.Delivery;
using Pizzeria.Domain.Menu;
using Pizzeria.Domain.Orders;
using Pizzeria.Domain.Tenancy;

namespace Pizzeria.Domain.Tests.Orders;

public sealed class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 18, 0, 0, TimeSpan.Zero);
    private static readonly Guid Staff = Guid.NewGuid();
    private static readonly Guid Driver = Guid.NewGuid();

    private readonly Location _location = Location.Create(
        Guid.NewGuid(), "Centrum", new Address("ul. Lipowa 1", "15-001", "Białystok"), null, Now);

    [Fact]
    public void Place_NewOrderStartsAsNewUnpaidWithHistoryAndTrackingToken()
    {
        var order = Delivery();

        Assert.Equal(OrderStatus.New, order.Status);
        Assert.Equal(PaymentStatus.Unpaid, order.PaymentStatus);
        Assert.Equal(FulfillmentType.Delivery, order.Fulfillment);
        var entry = Assert.Single(order.StatusHistory);
        Assert.Equal((null, OrderStatus.New), (entry.FromStatus, entry.ToStatus));
        Assert.True(order.TrackingToken.Length >= 32);
        Assert.NotEqual(order.TrackingToken, Delivery().TrackingToken);
    }

    [Fact]
    public void Place_ComputesTotalsFromLinesAndDeliveryFee()
    {
        var order = Delivery(lines: [Line(unit: 30m, quantity: 2, modifiers: 4m), Line(unit: 8m)], fee: 6.5m);

        Assert.Equal([68m, 8m], order.Items.Select(i => i.LineTotal));
        Assert.Equal(76m, order.ItemsTotal);
        Assert.Equal(82.5m, order.Total);
        Assert.Equal(34m, order.Items[0].UnitPrice);
        Assert.Equal("Ser", Assert.Single(order.Items[0].Modifiers).Name);
    }

    [Fact]
    public void Place_PickupHasNoDeliveryFeeOrAddress()
    {
        var order = Pickup();

        Assert.Equal(FulfillmentType.Pickup, order.Fulfillment);
        Assert.Null(order.DeliveryAddress);
        Assert.Equal(order.ItemsTotal, order.Total);
    }

    [Fact]
    public void Place_BelowZoneMinimum_Throws()
    {
        AssertFails(OrderErrors.BelowMinimumValue, () => Delivery(lines: [Line(unit: 20m)], minOrder: 30m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Place_InvalidQuantity_Throws(int quantity)
    {
        AssertFails(OrderErrors.InvalidQuantity, () => Delivery(lines: [Line(quantity: quantity)]));
    }

    [Fact]
    public void Place_WithoutItems_Throws()
    {
        AssertFails(OrderErrors.ItemsRequired, () => Delivery(lines: []));
    }

    [Fact]
    public void OnlineOrder_WaitsForPaymentBeforeItCanBeAccepted()
    {
        var order = Delivery(payment: PaymentMethod.Online);

        Assert.Equal(PaymentStatus.PendingOnline, order.PaymentStatus);
        Assert.True(order.AwaitingPayment);
        Assert.Null(order.NextStatus);
        AssertFails(OrderErrors.AwaitingPayment, () => order.Accept(Staff, Now));
    }

    [Fact]
    public void MarkPaidOnline_UnblocksAcceptanceAndIsIdempotent()
    {
        var order = Delivery(payment: PaymentMethod.Online);

        order.MarkPaidOnline(Now);
        order.MarkPaidOnline(Now);
        order.Accept(Staff, Now);

        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(OrderStatus.Accepted, order.Status);
        Assert.Single(order.StatusHistory, h => h.Reason == "paid online");
    }

    [Fact]
    public void MarkPaidOnline_ForCashOrder_Throws()
    {
        AssertFails(OrderErrors.PaymentMethodNotAvailable, () => Delivery().MarkPaidOnline(Now));
    }

    [Fact]
    public void OnlinePaidOrder_StaysPaidAfterDelivery()
    {
        var order = Delivery(payment: PaymentMethod.Online);
        order.MarkPaidOnline(Now);
        Advance(order, 6);

        Assert.Equal((OrderStatus.Delivered, PaymentStatus.Paid), (order.Status, order.PaymentStatus));
    }

    [Fact]
    public void DeliveryFlow_GoesThroughAllStatesAndRecordsWhoDidWhat()
    {
        var order = Delivery();

        order.Accept(Staff, Now.AddMinutes(1));
        order.StartPreparation(Staff, Now.AddMinutes(2));
        order.MarkReady(Staff, Now.AddMinutes(15));
        order.AssignDriver(Driver, Staff, Now.AddMinutes(16));
        order.StartDelivery(Driver, Now.AddMinutes(17));
        order.MarkDelivered(Driver, Now.AddMinutes(35));

        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal(Driver, order.DriverId);
        Assert.Equal(
            [OrderStatus.New, OrderStatus.Accepted, OrderStatus.InPreparation, OrderStatus.Ready,
             OrderStatus.AssignedToDriver, OrderStatus.InDelivery, OrderStatus.Delivered],
            order.StatusHistory.Select(h => h.ToStatus));
        Assert.Equal(Driver, order.StatusHistory[^1].ChangedByUserId);
        Assert.Equal(Now.AddMinutes(35), order.StatusHistory[^1].ChangedAt);
    }

    [Fact]
    public void PickupFlow_EndsWithPickedUp()
    {
        var order = Pickup();

        order.Accept(Staff, Now);
        order.StartPreparation(Staff, Now);
        order.MarkReady(Staff, Now);
        order.MarkPickedUp(Staff, Now);

        Assert.Equal(OrderStatus.PickedUp, order.Status);
    }

    [Theory]
    [InlineData(PaymentMethod.Cash, PaymentStatus.Paid)]
    [InlineData(PaymentMethod.CardOnDelivery, PaymentStatus.Paid)]
    public void Handover_SettlesPaymentCollectedOnDelivery(PaymentMethod method, PaymentStatus expected)
    {
        var order = Delivery(payment: method);
        MoveToReady(order);
        order.AssignDriver(Driver, Staff, Now);
        order.StartDelivery(Driver, Now);

        order.MarkDelivered(Driver, Now);

        Assert.Equal(expected, order.PaymentStatus);
    }

    [Fact]
    public void Pickup_CannotBeAssignedToDriver()
    {
        var order = Pickup();
        MoveToReady(order);

        AssertFails(OrderErrors.InvalidStatusTransition, () => order.AssignDriver(Driver, Staff, Now));
    }

    [Fact]
    public void Delivery_CannotBeMarkedPickedUp()
    {
        var order = Delivery();
        MoveToReady(order);

        AssertFails(OrderErrors.InvalidStatusTransition, () => order.MarkPickedUp(Staff, Now));
    }

    [Fact]
    public void SkippingAStep_Throws()
    {
        var order = Delivery();

        AssertFails(OrderErrors.InvalidStatusTransition, () => order.MarkReady(Staff, Now));
        Assert.Equal(OrderStatus.New, order.Status);
        Assert.Single(order.StatusHistory);
    }

    [Fact]
    public void ReassigningDriver_KeepsStatusAndIsRecorded()
    {
        var order = Delivery();
        MoveToReady(order);
        order.AssignDriver(Driver, Staff, Now);
        var otherDriver = Guid.NewGuid();

        order.AssignDriver(otherDriver, Staff, Now);

        Assert.Equal((OrderStatus.AssignedToDriver, otherDriver), (order.Status, order.DriverId));
        Assert.Equal((OrderStatus.AssignedToDriver, OrderStatus.AssignedToDriver), (order.StatusHistory[^1].FromStatus, order.StatusHistory[^1].ToStatus));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Cancel_IsAllowedBeforeHandover(int steps)
    {
        var order = Delivery();
        Advance(order, steps);

        order.Cancel("Klient zrezygnował", Staff, Now);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal("Klient zrezygnował", order.CancellationReason);
        Assert.Equal("Klient zrezygnował", order.StatusHistory[^1].Reason);
    }

    [Fact]
    public void Cancel_AfterDelivery_Throws()
    {
        var order = Delivery();
        Advance(order, 6);

        AssertFails(OrderErrors.InvalidStatusTransition, () => order.Cancel("Za późno", Staff, Now));
    }

    [Fact]
    public void Cancel_WithoutReason_Throws()
    {
        AssertFails(OrderErrors.CancellationReasonRequired, () => Delivery().Cancel("  ", Staff, Now));
    }

    [Fact]
    public void Cancelled_IsFinal()
    {
        var order = Delivery();
        order.Cancel("Brak ciasta", Staff, Now);

        AssertFails(OrderErrors.InvalidStatusTransition, () => order.Accept(Staff, Now));
        Assert.Null(order.NextStatus);
    }

    [Fact]
    public void Advance_FollowsNextStatusAndStopsWhereADriverIsNeeded()
    {
        var order = Delivery();

        Advance(order, 3);

        Assert.Equal(OrderStatus.Ready, order.Status);
        Assert.Null(order.NextStatus);
        AssertFails(OrderErrors.InvalidStatusTransition, () => order.Advance(Staff, Now));
    }

    private static void MoveToReady(Order order)
    {
        order.Accept(Staff, Now);
        order.StartPreparation(Staff, Now);
        order.MarkReady(Staff, Now);
    }

    /// <summary>0..6 steps along the delivery flow (step 4 assigns the driver).</summary>
    private static void Advance(Order order, int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            if (order.Status == OrderStatus.Ready)
            {
                order.AssignDriver(Driver, Staff, Now);
            }
            else
            {
                order.Advance(Staff, Now);
            }
        }
    }

    private Order Delivery(
        IReadOnlyList<OrderLine>? lines = null,
        decimal fee = 5m,
        decimal minOrder = 0m,
        PaymentMethod payment = PaymentMethod.Cash) =>
        Order.Place(
            _location,
            1,
            OrderSource.Online,
            new OrderCustomer("Jan Kowalski", "600 100 200"),
            new DeliveryTerms(new Address("ul. Rynek 1", "15-001", "Białystok"), new GeoPoint(53.13, 23.16), "domofon 12", fee, minOrder),
            lines ?? [Line()],
            payment,
            null,
            null,
            Now);

    private Order Pickup() =>
        Order.Place(_location, 2, OrderSource.Phone, new OrderCustomer("Anna", "600100200"), null, [Line()], PaymentMethod.Cash, null, Staff, Now);

    private static OrderLine Line(decimal unit = 30m, int quantity = 1, decimal? modifiers = null) =>
        new(
            Guid.NewGuid(),
            "Margherita",
            null,
            null,
            Guid.NewGuid(),
            "32 cm",
            quantity,
            new PricedItem(unit, modifiers is { } m ? [new PricedModifier(Guid.NewGuid(), Guid.NewGuid(), "Ser", m)] : []),
            null);

    private static void AssertFails(string code, Func<object?> act)
    {
        var ex = Assert.Throws<DomainException>(act);
        Assert.Equal(code, ex.Code);
    }

    private static void AssertFails(string code, Action act)
    {
        var ex = Assert.Throws<DomainException>(act);
        Assert.Equal(code, ex.Code);
    }
}
