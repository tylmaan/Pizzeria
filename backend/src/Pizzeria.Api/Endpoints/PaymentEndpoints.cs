using Pizzeria.Application.Payments;
using Pizzeria.Infrastructure.Payments;

namespace Pizzeria.Api.Endpoints;

public sealed record FakePaymentCompletion(bool Succeeded);

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder routes, IConfiguration configuration)
    {
        // Called by the payment provider: anonymous, trusted only through the signature check.
        routes.MapPost("/payments/webhooks/{provider}", async (string provider, HttpRequest request, PaymentService payments, CancellationToken ct) =>
            {
                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
                var headers = request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);
                await payments.HandleNotificationAsync(provider, body, headers, ct).ConfigureAwait(false);
                return TypedResults.Ok();
            })
            .WithTags("Payments")
            .AllowAnonymous();

        // Customer retries an unpaid online order from the tracking page.
        routes.MapPost("/shop/orders/{token}/payment", (string token, PaymentService payments, CancellationToken ct) =>
                payments.RetryAsync(token, ct))
            .WithTags("Shop")
            .AllowAnonymous();

        // Development only: the fake payment page "pays" by sending a correctly signed webhook.
        var provider = configuration.GetValue<string>($"{PaymentOptions.SectionName}:Provider");
        if (string.Equals(provider, FakePaymentProvider.ProviderName, StringComparison.OrdinalIgnoreCase))
        {
            routes.MapPost("/payments/fake/{providerPaymentId}/complete", async (
                    string providerPaymentId, FakePaymentCompletion completion, FakePaymentProvider fake, PaymentService payments, CancellationToken ct) =>
                {
                    var body = FakePaymentProvider.Body(providerPaymentId, completion.Succeeded);
                    var headers = new Dictionary<string, string> { [FakePaymentProvider.SignatureHeader] = fake.Sign(body) };
                    await payments.HandleNotificationAsync(FakePaymentProvider.ProviderName, body, headers, ct).ConfigureAwait(false);
                    return TypedResults.Ok();
                })
                .WithTags("Payments")
                .AllowAnonymous();
        }

        return routes;
    }
}
