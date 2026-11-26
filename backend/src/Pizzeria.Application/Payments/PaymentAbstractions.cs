namespace Pizzeria.Application.Payments;

/// <param name="PaymentId">Our payment attempt id (sent to the provider as the merchant reference).</param>
/// <param name="Amount">Amount in PLN.</param>
/// <param name="Description">Shown to the customer by the provider, e.g. "Zamówienie nr 12".</param>
/// <param name="ReturnPath">Where the provider sends the customer back, relative to the shop (e.g. <c>/order/{token}</c>).</param>
public sealed record ProviderPaymentRequest(Guid PaymentId, decimal Amount, string Description, string ReturnPath);

/// <param name="ProviderPaymentId">The provider's id of the payment.</param>
/// <param name="RedirectUrl">Where to send the customer to pay.</param>
public sealed record ProviderPayment(string ProviderPaymentId, string RedirectUrl);

/// <summary>A verified notification from the provider.</summary>
public sealed record ProviderNotification(string ProviderPaymentId, bool Succeeded);

/// <summary>
/// An online payment operator (Przelewy24 / PayU — to be decided; a fake one for development).
/// Every company has its own account at the operator, so credentials are per company.
/// </summary>
public interface IPaymentProvider
{
    string Name { get; }

    Task<ProviderPayment> CreateAsync(ProviderPaymentRequest request, CancellationToken ct);

    /// <summary>Checks the webhook signature and parses it; <c>null</c> when the signature is not valid.</summary>
    ProviderNotification? Verify(string body, IReadOnlyDictionary<string, string> headers);
}

/// <summary>Which provider a company uses (null = online payments are not available).</summary>
public interface IPaymentProviderResolver
{
    IPaymentProvider? ForCompany(Guid companyId);

    IPaymentProvider? ByName(string name);
}
