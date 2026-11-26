using Pizzeria.Domain.Common;
using Pizzeria.Domain.Orders;
using Pizzeria.Domain.Printing;

namespace Pizzeria.Domain.Tests.Printing;

public sealed class PrintingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 18, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(OrderStatus.Accepted, true, FiscalReceiptTrigger.Never, new[] { PrintJobKind.KitchenTicket })]
    [InlineData(OrderStatus.Accepted, false, FiscalReceiptTrigger.Never, new PrintJobKind[0])]
    [InlineData(OrderStatus.Accepted, true, FiscalReceiptTrigger.OnAccepted, new[] { PrintJobKind.KitchenTicket, PrintJobKind.FiscalReceipt })]
    [InlineData(OrderStatus.Accepted, true, FiscalReceiptTrigger.OnCompleted, new[] { PrintJobKind.KitchenTicket })]
    [InlineData(OrderStatus.Delivered, true, FiscalReceiptTrigger.OnCompleted, new[] { PrintJobKind.FiscalReceipt })]
    [InlineData(OrderStatus.PickedUp, true, FiscalReceiptTrigger.OnCompleted, new[] { PrintJobKind.FiscalReceipt })]
    [InlineData(OrderStatus.Ready, true, FiscalReceiptTrigger.OnCompleted, new PrintJobKind[0])]
    [InlineData(OrderStatus.Delivered, true, FiscalReceiptTrigger.Never, new PrintJobKind[0])]
    public void DueOn_FollowsLocationSettings(OrderStatus status, bool kitchen, FiscalReceiptTrigger fiscal, PrintJobKind[] expected)
    {
        Assert.Equal(expected, PrintRules.DueOn(status, kitchen, fiscal));
    }

    [Fact]
    public void Agent_TokenIsStoredOnlyAsHash()
    {
        var (agent, token) = PrintAgent.Register(Guid.NewGuid(), Guid.NewGuid(), " Komputer w kuchni ", Now);

        Assert.Equal("Komputer w kuchni", agent.Name);
        Assert.True(token.Length >= 40);
        Assert.NotEqual(token, agent.TokenHash);
        Assert.Equal(PrintAgent.HashToken(token), agent.TokenHash);
    }

    [Fact]
    public void Agent_IsOnlineOnlyShortlyAfterHeartbeat_AndNeverWhenRevoked()
    {
        var (agent, _) = PrintAgent.Register(Guid.NewGuid(), Guid.NewGuid(), "Kasa", Now);
        Assert.False(agent.IsOnline(Now));

        agent.ReportStatus(true, null, Now);

        Assert.True(agent.IsOnline(Now.AddSeconds(60)));
        Assert.False(agent.IsOnline(Now.AddMinutes(5)));
        agent.Revoke();
        Assert.False(agent.IsOnline(Now));
        Assert.Throws<DomainException>(() => agent.ReportStatus(true, true, Now));
    }

    [Fact]
    public void Job_LifecycleCountsAttemptsAndIgnoresLateFailures()
    {
        var job = PrintJob.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PrintJobKind.KitchenTicket, "{}", Now);

        job.MarkSent(Now);
        job.MarkSent(Now.AddMinutes(3)); // delivered again after a reconnect
        job.MarkPrinted(Now.AddMinutes(3));
        job.MarkFailed("paper out");

        Assert.Equal((PrintJobStatus.Printed, 2, (string?)null), (job.Status, job.Attempts, job.Error));
    }

    [Fact]
    public void Job_FailedCanBeRetried_OthersCannot()
    {
        var job = PrintJob.Create(Guid.NewGuid(), Guid.NewGuid(), null, PrintJobKind.KitchenTicket, "{}", Now);
        Assert.Throws<DomainException>(job.Retry);

        job.MarkSent(Now);
        job.MarkFailed("  Brak papieru  ");
        Assert.Equal("Brak papieru", job.Error);
        job.Retry();

        Assert.Equal((PrintJobStatus.Pending, (string?)null), (job.Status, job.Error));
    }
}
