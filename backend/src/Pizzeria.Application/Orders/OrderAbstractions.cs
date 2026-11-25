using Pizzeria.Domain.Orders;

namespace Pizzeria.Application.Orders;

/// <summary>Short order numbers: 1, 2, 3… per location, starting again every business day.</summary>
public interface IOrderNumberGenerator
{
    Task<int> NextAsync(Guid locationId, DateOnly businessDay, CancellationToken ct);
}

/// <summary>
/// Tells live screens (order queue, kitchen, customer tracking) that an order changed. Only a notification:
/// clients reload the current state from the REST API.
/// </summary>
public interface IOrderNotifier
{
    Task OrderChangedAsync(Order order, CancellationToken ct);
}

public sealed class NullOrderNotifier : IOrderNotifier
{
    public Task OrderChangedAsync(Order order, CancellationToken ct) => Task.CompletedTask;
}
