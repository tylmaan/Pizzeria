using Microsoft.EntityFrameworkCore;
using Pizzeria.Application.Abstractions;
using Pizzeria.Application.Errors;
using Pizzeria.Application.Orders;
using Pizzeria.Application.Tenancy;
using Pizzeria.Domain.Orders;
using Pizzeria.Domain.Payments;

namespace Pizzeria.Application.Payments;

public sealed record PaymentRedirectDto(string RedirectUrl);

/// <summary>Starts online payments of orders and applies the provider's notifications.</summary>
public sealed class PaymentService(
    IAppDbContext db,
    TenantContext tenant,
    TimeProvider clock,
    IPaymentProviderResolver providers,
    IOrderNotifier notifier)
{
    /// <summary>Creates a payment attempt at the company's provider; the customer is redirected to pay.</summary>
    public async Task<PaymentRedirectDto> StartAsync(Order order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);

        var provider = providers.ForCompany(order.CompanyId)
            ?? throw new UnprocessableException(OrderErrors.PaymentMethodNotAvailable);

        var payment = Payment.Start(order, provider.Name, clock.GetUtcNow());
        db.Payments.Add(payment);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var created = await provider.CreateAsync(
            new ProviderPaymentRequest(payment.Id, payment.Amount, $"Zamówienie nr {order.Number}", $"/order/{order.TrackingToken}"),
            ct).ConfigureAwait(false);
        payment.AttachProviderPaymentId(created.ProviderPaymentId);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new PaymentRedirectDto(created.RedirectUrl);
    }

    /// <summary>Customer retries after a failed or abandoned payment (from the tracking page).</summary>
    public async Task<PaymentRedirectDto> RetryAsync(string trackingToken, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.TrackingToken == trackingToken, ct).ConfigureAwait(false)
            ?? throw new NotFoundException(ErrorCodes.OrderNotFound);
        if (order.Status == OrderStatus.Cancelled)
        {
            throw new UnprocessableException(OrderErrors.InvalidStatusTransition);
        }

        return await StartAsync(order, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Applies a provider webhook. Safe to receive the same notification many times: a completed attempt
    /// is not changed again and an already paid order stays paid.
    /// </summary>
    public async Task HandleNotificationAsync(string providerName, string body, IReadOnlyDictionary<string, string> headers, CancellationToken ct)
    {
        var provider = providers.ByName(providerName) ?? throw new NotFoundException(ErrorCodes.PaymentProviderNotFound);
        var notification = provider.Verify(body, headers) ?? throw new ForbiddenException(ErrorCodes.InvalidSignature);

        // Webhooks come from the provider, not from a signed-in user or a shop host: there is no tenant yet.
        // Find the attempt across tenants, then act strictly within its company.
        var payment = await db.Payments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Provider == provider.Name && p.ProviderPaymentId == notification.ProviderPaymentId, ct)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(ErrorCodes.PaymentNotFound);
        tenant.SetSystemForCompany(payment.CompanyId);

        if (!payment.Complete(notification.Succeeded, clock.GetUtcNow()))
        {
            return;
        }

        var order = await db.Orders.FirstAsync(o => o.Id == payment.OrderId, ct).ConfigureAwait(false);
        if (notification.Succeeded)
        {
            order.MarkPaidOnline(clock.GetUtcNow());
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await notifier.OrderChangedAsync(order, ct).ConfigureAwait(false);
    }
}
