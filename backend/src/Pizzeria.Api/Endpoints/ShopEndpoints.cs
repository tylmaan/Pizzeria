using Pizzeria.Application.Orders;
using Pizzeria.Domain.Orders;

namespace Pizzeria.Api.Endpoints;

/// <summary>Customer shop: anonymous, the company comes from the host name (e.g. dominos.platform.pl).</summary>
public static class ShopEndpoints
{
    public const string PlaceOrderRateLimitPolicy = "place-order";

    public static IEndpointRouteBuilder MapShopEndpoints(this IEndpointRouteBuilder routes)
    {
        var shop = routes.MapGroup("/shop").WithTags("Shop").AllowAnonymous();

        shop.MapGet("/", (ShopService service, CancellationToken ct) => service.GetAsync(ct));

        shop.MapPost("/orders", async (PlaceOrderRequest request, PlaceOrderService service, CancellationToken ct) =>
            {
                var order = await service.PlaceAsync(request, OrderSource.Online, ct).ConfigureAwait(false);
                return TypedResults.Created($"/api/shop/orders/{order.TrackingToken}", order);
            })
            .RequireRateLimiting(PlaceOrderRateLimitPolicy);

        shop.MapGet("/orders/{token}", (string token, OrderTrackingService service, CancellationToken ct) =>
            service.GetAsync(token, ct));

        return routes;
    }
}
