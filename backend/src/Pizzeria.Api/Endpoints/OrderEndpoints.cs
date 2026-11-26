using Pizzeria.Api.Auth;
using Pizzeria.Application.Errors;
using Pizzeria.Application.Orders;
using Pizzeria.Application.Tenancy;
using Pizzeria.Domain.Identity;
using Pizzeria.Domain.Orders;

namespace Pizzeria.Api.Endpoints;

/// <summary>Orders for staff, kitchen and drivers. Location access and per-status roles are checked in Application.</summary>
public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder routes)
    {
        var location = routes.MapGroup("/locations/{locationId:guid}")
            .WithTags("Orders")
            .RequireAuthorization(AuthorizationPolicies.CompanyStaff);

        location.MapGet("/orders", (Guid locationId, OrderListScope? scope, OrderQueueService service, CancellationToken ct) =>
            service.ListAsync(locationId, scope ?? OrderListScope.Active, ct));

        location.MapGet("/drivers", (Guid locationId, OrderWorkflowService service, CancellationToken ct) =>
            service.ListDriversAsync(locationId, ct));

        // Phone / in-store orders taken by staff.
        location.MapPost("/orders", async (Guid locationId, PlaceOrderRequest request, PlaceOrderService service, ITenantContext tenant, CancellationToken ct) =>
        {
            var mayTakeOrders = tenant.IsCompanyAdmin || tenant.IsInRole(Roles.LocationManager) || tenant.IsInRole(Roles.Staff);
            if (!mayTakeOrders || !tenant.CanAccessLocation(locationId))
            {
                throw new NotFoundException(ErrorCodes.LocationNotFound);
            }

            var order = await service.PlaceAsync(request, OrderSource.Phone, ct).ConfigureAwait(false);
            return TypedResults.Created($"/api/orders/{order.Id}", order);
        });

        var orders = routes.MapGroup("/orders/{id:guid}")
            .WithTags("Orders")
            .RequireAuthorization(AuthorizationPolicies.CompanyStaff);

        orders.MapGet("/", (Guid id, OrderQueueService service, CancellationToken ct) => service.GetAsync(id, ct));

        orders.MapPost("/advance", (Guid id, OrderWorkflowService service, CancellationToken ct) => service.AdvanceAsync(id, ct));

        orders.MapPost("/cancel", (Guid id, CancelOrderRequest request, OrderWorkflowService service, CancellationToken ct) =>
            service.CancelAsync(id, request, ct));

        orders.MapPost("/driver", (Guid id, AssignDriverRequest request, OrderWorkflowService service, CancellationToken ct) =>
            service.AssignDriverAsync(id, request, ct));

        return routes;
    }
}
