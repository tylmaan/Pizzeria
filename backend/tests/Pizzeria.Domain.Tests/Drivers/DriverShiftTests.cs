using Pizzeria.Domain.Common;
using Pizzeria.Domain.Drivers;
using Pizzeria.Domain.Orders;

namespace Pizzeria.Domain.Tests.Drivers;

public sealed class DriverShiftTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 26, 16, 0, 0, TimeSpan.Zero);
    private static readonly Guid Manager = Guid.NewGuid();

    [Fact]
    public void Start_OpensShiftWithChangeMoney()
    {
        var shift = NewShift(startingCash: 100m);

        Assert.Equal(DriverShiftStatus.Open, shift.Status);
        Assert.Equal(100m, shift.StartingCash);
        Assert.Equal(100m, shift.ExpectedCash);
        Assert.Null(shift.Discrepancy);
    }

    [Fact]
    public void Start_WhenDriverAlreadyHasOpenShift_Throws()
    {
        var ex = Assert.Throws<DomainException>(() =>
            DriverShift.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 50m, hasOpenShift: true, Start));

        Assert.Equal(DriverShiftErrors.AlreadyOpen, ex.Code);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0.001)]
    public void Start_InvalidChangeMoney_Throws(decimal startingCash)
    {
        var ex = Assert.Throws<DomainException>(() => NewShift(startingCash));

        Assert.Equal(DriverShiftErrors.InvalidAmount, ex.Code);
    }

    [Fact]
    public void Close_TotalsDeliveriesPerPaymentMethod()
    {
        var shift = NewShift(startingCash: 100m);

        shift.Close(
            [
                new(PaymentMethod.Cash, 74m),
                new(PaymentMethod.Cash, 52.5m),
                new(PaymentMethod.CardOnDelivery, 61m),
                new(PaymentMethod.Online, 40m),
            ],
            tips: 12m,
            Start.AddHours(6));

        Assert.Equal(DriverShiftStatus.Closed, shift.Status);
        Assert.Equal(4, shift.DeliveriesCount);
        Assert.Equal((126.5m, 61m, 40m, 12m), (shift.CashCollected, shift.CardCollected, shift.PaidOnline, shift.Tips));
        Assert.Equal(226.5m, shift.ExpectedCash); // change money + cash collected; tips stay with the driver
        Assert.Equal(Start.AddHours(6), shift.EndedAt);
    }

    [Fact]
    public void Close_Twice_Throws()
    {
        var shift = NewShift();
        shift.Close([], 0m, Start);

        var ex = Assert.Throws<DomainException>(() => shift.Close([], 0m, Start));

        Assert.Equal(DriverShiftErrors.NotOpen, ex.Code);
    }

    [Fact]
    public void Settle_ExactAmount_NeedsNoComment()
    {
        var shift = ClosedShift(startingCash: 100m, cash: 74m);

        shift.Settle(174m, null, Manager, Start.AddHours(7));

        Assert.Equal(DriverShiftStatus.Settled, shift.Status);
        Assert.Equal(0m, shift.Discrepancy);
        Assert.Equal(Manager, shift.SettledByUserId);
    }

    [Theory]
    [InlineData(170, -4)]
    [InlineData(180, 6)]
    public void Settle_DifferenceIsRecordedWithComment(decimal counted, decimal expectedDiscrepancy)
    {
        var shift = ClosedShift(startingCash: 100m, cash: 74m);

        shift.Settle(counted, "  Brak drobnych  ", Manager, Start.AddHours(7));

        Assert.Equal(expectedDiscrepancy, shift.Discrepancy);
        Assert.Equal("Brak drobnych", shift.DiscrepancyComment);
    }

    [Fact]
    public void Settle_DifferenceWithoutComment_Throws()
    {
        var shift = ClosedShift(startingCash: 100m, cash: 74m);

        var ex = Assert.Throws<DomainException>(() => shift.Settle(170m, " ", Manager, Start));

        Assert.Equal(DriverShiftErrors.CommentRequired, ex.Code);
        Assert.Equal(DriverShiftStatus.Closed, shift.Status);
    }

    [Fact]
    public void Settle_OpenShift_Throws()
    {
        var ex = Assert.Throws<DomainException>(() => NewShift().Settle(0m, null, Manager, Start));

        Assert.Equal(DriverShiftErrors.NotClosed, ex.Code);
    }

    private static DriverShift NewShift(decimal startingCash = 50m) =>
        DriverShift.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), startingCash, hasOpenShift: false, Start);

    private static DriverShift ClosedShift(decimal startingCash, decimal cash)
    {
        var shift = NewShift(startingCash);
        shift.Close([new(PaymentMethod.Cash, cash)], 0m, Start.AddHours(6));
        return shift;
    }
}
