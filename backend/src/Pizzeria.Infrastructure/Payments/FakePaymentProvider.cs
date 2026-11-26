using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Pizzeria.Application.Payments;

namespace Pizzeria.Infrastructure.Payments;

public sealed class PaymentOptions
{
    public const string SectionName = "Payments";

    /// <summary>
    /// Provider for all companies. Empty = online payments switched off (the default outside development).
    /// The real operator (Przelewy24 / PayU) and per-company credentials are an open decision.
    /// </summary>
    public string? Provider { get; set; }

    public FakePaymentOptions Fake { get; set; } = new();
}

public sealed class FakePaymentOptions
{
    /// <summary>Signs fake webhooks. Development only, not a real secret.</summary>
    public string WebhookSecret { get; set; } = "dev-only-fake-payment-secret";
}

/// <summary>
/// Development stand-in for a payment operator: the "payment page" is a page of our own shop
/// (<c>/pay/fake/{id}</c>) whose buttons trigger a webhook signed the way a real provider signs it.
/// </summary>
public sealed class FakePaymentProvider(IOptions<PaymentOptions> options) : IPaymentProvider
{
    public const string ProviderName = "Fake";
    public const string SignatureHeader = "X-Fake-Signature";

    public string Name => ProviderName;

    public Task<ProviderPayment> CreateAsync(ProviderPaymentRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var id = $"fake_{request.PaymentId:N}";
        var amount = request.Amount.ToString("0.00", CultureInfo.InvariantCulture);
        var url = $"/pay/fake/{id}?amount={amount}&return={Uri.EscapeDataString(request.ReturnPath)}";
        return Task.FromResult(new ProviderPayment(id, url));
    }

    public ProviderNotification? Verify(string body, IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(headers);

        if (!headers.TryGetValue(SignatureHeader, out var signature)
            || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(signature), Encoding.ASCII.GetBytes(Sign(body))))
        {
            return null;
        }

        var message = JsonSerializer.Deserialize<FakeWebhook>(body, JsonSerializerOptions.Web);
        return message is null ? null : new ProviderNotification(message.PaymentId, message.Status == "succeeded");
    }

    /// <summary>Body of the webhook the fake operator sends.</summary>
    public static string Body(string providerPaymentId, bool succeeded) =>
        JsonSerializer.Serialize(new FakeWebhook(providerPaymentId, succeeded ? "succeeded" : "failed"), JsonSerializerOptions.Web);

    public string Sign(string body) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.Value.Fake.WebhookSecret), Encoding.UTF8.GetBytes(body)));

    private sealed record FakeWebhook(string PaymentId, string Status);
}

/// <summary>For now one configured provider serves every company; per-company accounts come with the real operator.</summary>
public sealed class ConfiguredPaymentProviderResolver(IEnumerable<IPaymentProvider> providers, IOptions<PaymentOptions> options)
    : IPaymentProviderResolver
{
    public IPaymentProvider? ForCompany(Guid companyId) =>
        string.IsNullOrWhiteSpace(options.Value.Provider) ? null : ByName(options.Value.Provider);

    public IPaymentProvider? ByName(string name) =>
        providers.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
}
