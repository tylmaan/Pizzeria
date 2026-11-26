using Pizzeria.Domain.Common;
using Pizzeria.Domain.Orders;

namespace Pizzeria.Domain.Payments;

public enum PaymentAttemptStatus
{
    Pending,
    Succeeded,
    Failed,
}

public static class PaymentErrors
{
    public const string NotOnlineOrder = "payment.not_online_order";
    public const string AlreadyPaid = "payment.already_paid";
}

/// <summary>
/// One attempt to pay an order online at the company's payment provider. An order can have several
/// attempts (the customer retries after a failure); the provider's webhook completes an attempt.
/// </summary>
public sealed class Payment : ILocationEntity
{
    public const int ProviderMaxLength = 30;
    public const int ProviderPaymentIdMaxLength = 100;

    private Payment()
    {
    }

    public Guid Id { get; private set; }

    public Guid CompanyId { get; private set; }

    public Guid LocationId { get; private set; }

    public Guid OrderId { get; private set; }

    /// <summary>Provider name, e.g. <c>Fake</c>, <c>Przelewy24</c>, <c>PayU</c>.</summary>
    public string Provider { get; private set; } = null!;

    /// <summary>The provider's id of this payment (known after the provider created it).</summary>
    public string? ProviderPaymentId { get; private set; }

    public decimal Amount { get; private set; }

    public PaymentAttemptStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public static Payment Start(Order order, string provider, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);

        if (order.PaymentMethod != PaymentMethod.Online)
        {
            throw new DomainException(PaymentErrors.NotOnlineOrder);
        }

        if (order.PaymentStatus != PaymentStatus.PendingOnline)
        {
            throw new DomainException(PaymentErrors.AlreadyPaid);
        }

        return new Payment
        {
            Id = Guid.CreateVersion7(now),
            CompanyId = order.CompanyId,
            LocationId = order.LocationId,
            OrderId = order.Id,
            Provider = provider,
            Amount = order.Total,
            Status = PaymentAttemptStatus.Pending,
            CreatedAt = now,
        };
    }

    public void AttachProviderPaymentId(string providerPaymentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerPaymentId);
        ProviderPaymentId = providerPaymentId;
    }

    /// <summary>
    /// Applies the provider's verdict. Returns <c>false</c> when the attempt was already completed —
    /// providers resend webhooks, and processing the same one twice must change nothing.
    /// </summary>
    public bool Complete(bool succeeded, DateTimeOffset now)
    {
        if (Status != PaymentAttemptStatus.Pending)
        {
            return false;
        }

        Status = succeeded ? PaymentAttemptStatus.Succeeded : PaymentAttemptStatus.Failed;
        CompletedAt = now;
        return true;
    }
}
