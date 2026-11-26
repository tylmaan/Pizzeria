using Pizzeria.Domain.Common;
using Pizzeria.Domain.Menu;
using Pizzeria.Domain.Orders;
using Pizzeria.Domain.Payments;
using Pizzeria.Domain.Tenancy;

namespace Pizzeria.Domain.Tests.Payments;

public sealed class PaymentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Start_TakesAmountAndTenantFromOrder()
    {
        var order = Order(PaymentMethod.Online);

        var payment = Payment.Start(order, "Fake", Now);

        Assert.Equal((order.Id, order.CompanyId, order.LocationId, 30m), (payment.OrderId, payment.CompanyId, payment.LocationId, payment.Amount));
        Assert.Equal(PaymentAttemptStatus.Pending, payment.Status);
    }

    [Fact]
    public void Start_ForCashOrder_Throws()
    {
        var ex = Assert.Throws<DomainException>(() => Payment.Start(Order(PaymentMethod.Cash), "Fake", Now));

        Assert.Equal(PaymentErrors.NotOnlineOrder, ex.Code);
    }

    [Fact]
    public void Start_ForAlreadyPaidOrder_Throws()
    {
        var order = Order(PaymentMethod.Online);
        order.MarkPaidOnline(Now);

        var ex = Assert.Throws<DomainException>(() => Payment.Start(order, "Fake", Now));

        Assert.Equal(PaymentErrors.AlreadyPaid, ex.Code);
    }

    [Fact]
    public void Complete_SecondTimeChangesNothing()
    {
        var payment = Payment.Start(Order(PaymentMethod.Online), "Fake", Now);

        Assert.True(payment.Complete(succeeded: false, Now));
        Assert.False(payment.Complete(succeeded: true, Now.AddMinutes(1)));

        Assert.Equal((PaymentAttemptStatus.Failed, Now), (payment.Status, payment.CompletedAt!.Value));
    }

    private static Order Order(PaymentMethod method)
    {
        var location = Location.Create(Guid.NewGuid(), "Centrum", new Address("ul. Lipowa 1", "15-001", "Białystok"), null, Now);
        return Domain.Orders.Order.Place(location, 1, OrderSource.Online, new OrderCustomer("Jan", "600100200"), null,
            [new OrderLine(Guid.NewGuid(), "Margherita", null, null, Guid.NewGuid(), "32 cm", 1, new PricedItem(30m, []), null)],
            method, null, null, Now);
    }
}
