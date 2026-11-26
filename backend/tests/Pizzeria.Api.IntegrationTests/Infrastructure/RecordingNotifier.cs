using System.Collections.Concurrent;
using Pizzeria.Application.Orders;
using Pizzeria.Domain.Orders;

namespace Pizzeria.Api.IntegrationTests.Infrastructure;

/// <summary>Records every order notification and passes it on to the real (SignalR) notifier.</summary>
public sealed class RecordingNotifier(IOrderNotifier inner) : IOrderNotifier
{
    private readonly ConcurrentQueue<(Guid OrderId, OrderStatus Status)> _sent = new();

    public IReadOnlyList<OrderStatus> For(Guid orderId) => [.. _sent.Where(s => s.OrderId == orderId).Select(s => s.Status)];

    public Task OrderChangedAsync(Order order, CancellationToken ct)
    {
        _sent.Enqueue((order.Id, order.Status));
        return inner.OrderChangedAsync(order, ct);
    }
}
