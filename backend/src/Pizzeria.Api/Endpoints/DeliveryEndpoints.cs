using Pizzeria.Api.Auth;
using Pizzeria.Application.Delivery;

namespace Pizzeria.Api.Endpoints;

public static class DeliveryEndpoints
{
    public const string DeliveryCheckRateLimitPolicy = "delivery-check";

    public static IEndpointRouteBuilder MapDeliveryEndpoints(this IEndpointRouteBuilder routes)
    {
        var zones = routes.MapGroup("/locations/{locationId:guid}/delivery-zones")
            .WithTags("Delivery")
            .RequireAuthorization(AuthorizationPolicies.StaffManagement);

        zones.MapGet("/", (Guid locationId, DeliveryZoneService service, CancellationToken ct) =>
            service.ListAsync(locationId, ct));

        zones.MapPost("/", async (Guid locationId, SaveDeliveryZoneRequest request, DeliveryZoneService service, CancellationToken ct) =>
        {
            var zone = await service.CreateAsync(locationId, request, ct).ConfigureAwait(false);
            return TypedResults.Created($"/api/locations/{locationId}/delivery-zones/{zone.Id}", zone);
        });

        zones.MapPut("/{id:guid}", (Guid locationId, Guid id, SaveDeliveryZoneRequest request, DeliveryZoneService service, CancellationToken ct) =>
            service.UpdateAsync(locationId, id, request, ct));

        // Used by the customer shop (anonymous, tenant from the host name) and by staff taking phone orders.
        // Rate limited: with a real geocoding provider every call costs money.
        routes.MapPost("/delivery/check", (DeliveryCheckRequest request, DeliveryCheckService service, CancellationToken ct) =>
                service.CheckAsync(request, ct))
            .WithTags("Delivery")
            .AllowAnonymous()
            .RequireRateLimiting(DeliveryCheckRateLimitPolicy);

        return routes;
    }
}
