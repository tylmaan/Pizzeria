using Pizzeria.Application.Orders;
using Pizzeria.Application.Tenancy;
using Pizzeria.Domain.Identity;
using Pizzeria.Domain.Menu;
using Pizzeria.Domain.Orders;
using Pizzeria.Domain.Tenancy;

namespace Pizzeria.Application.Tests.Orders;

public sealed class OrderPermissionsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 18, 0, 0, TimeSpan.Zero);
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Location Location = Domain.Tenancy.Location.Create(
        CompanyId, "Centrum", new Address("ul. Lipowa 1", "15-001", "Białystok"), null, Now);

    [Theory]
    [InlineData(Roles.Staff, OrderStatus.Accepted, true)]
    [InlineData(Roles.Staff, OrderStatus.Cancelled, true)]
    [InlineData(Roles.LocationManager, OrderStatus.AssignedToDriver, true)]
    [InlineData(Roles.Kitchen, OrderStatus.InPreparation, true)]
    [InlineData(Roles.Kitchen, OrderStatus.Ready, true)]
    [InlineData(Roles.Kitchen, OrderStatus.Accepted, false)]
    [InlineData(Roles.Kitchen, OrderStatus.Cancelled, false)]
    [InlineData(Roles.Kitchen, OrderStatus.AssignedToDriver, false)]
    [InlineData(Roles.Driver, OrderStatus.Accepted, false)]
    [InlineData(Roles.Driver, OrderStatus.Cancelled, false)]
    public void CanMoveTo_FollowsRoles(string role, OrderStatus target, bool expected)
    {
        Assert.Equal(expected, OrderPermissions.CanMoveTo(User(role), Order(), target));
    }

    [Fact]
    public void Driver_MayOnlyDeliverOwnOrders()
    {
        var driver = User(Roles.Driver);
        var order = Order();
        MoveToReady(order);
        order.AssignDriver(driver.UserId!.Value, null, Now);

        Assert.True(OrderPermissions.CanMoveTo(driver, order, OrderStatus.InDelivery));
        Assert.True(OrderPermissions.CanView(driver, order));
        Assert.False(OrderPermissions.CanMoveTo(User(Roles.Driver), order, OrderStatus.InDelivery));
        Assert.False(OrderPermissions.CanView(User(Roles.Driver), order));
    }

    [Fact]
    public void StaffOfAnotherLocation_CannotViewOrder()
    {
        var other = new TenantContext();
        other.SetUser(Guid.NewGuid(), CompanyId, [Roles.Staff], [Guid.NewGuid()]);

        Assert.False(OrderPermissions.CanView(other, Order()));
    }

    private static TenantContext User(string role)
    {
        var user = new TenantContext();
        user.SetUser(Guid.NewGuid(), CompanyId, [role], [Location.Id]);
        return user;
    }

    private static void MoveToReady(Order order)
    {
        order.Accept(null, Now);
        order.StartPreparation(null, Now);
        order.MarkReady(null, Now);
    }

    private static Order Order() =>
        Domain.Orders.Order.Place(
            Location,
            1,
            OrderSource.Online,
            new OrderCustomer("Jan", "600100200"),
            new DeliveryTerms(new Address("ul. Rynek 1", "15-001", "Białystok"), null, null, 5m, 0m),
            [new OrderLine(Guid.NewGuid(), "Margherita", null, null, Guid.NewGuid(), "32 cm", 1, new PricedItem(25m, []), null)],
            PaymentMethod.Cash,
            null,
            null,
            Now);
}
