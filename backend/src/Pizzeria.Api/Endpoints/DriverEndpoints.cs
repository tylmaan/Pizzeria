using Pizzeria.Api.Auth;
using Pizzeria.Application.Drivers;
using Pizzeria.Domain.Drivers;

namespace Pizzeria.Api.Endpoints;

public static class DriverEndpoints
{
    public static IEndpointRouteBuilder MapDriverEndpoints(this IEndpointRouteBuilder routes)
    {
        var mine = routes.MapGroup("/driver/shifts")
            .WithTags("Drivers")
            .RequireAuthorization(AuthorizationPolicies.Driver);

        mine.MapGet("/current", (DriverShiftService service, CancellationToken ct) => service.GetCurrentAsync(ct));

        mine.MapPost("/", async (StartShiftRequest request, DriverShiftService service, CancellationToken ct) =>
        {
            var shift = await service.StartAsync(request, ct).ConfigureAwait(false);
            return TypedResults.Created("/api/driver/shifts/current", shift);
        });

        routes.MapPost("/driver/position", async (ReportPositionRequest request, DriverTrackingService service, CancellationToken ct) =>
            {
                await service.RecordAsync(request, ct).ConfigureAwait(false);
                return TypedResults.NoContent();
            })
            .WithTags("Drivers")
            .RequireAuthorization(AuthorizationPolicies.Driver);

        mine.MapPost("/current/close", (CloseShiftRequest request, DriverShiftService service, CancellationToken ct) =>
            service.CloseAsync(request, ct));

        var settlement = routes.MapGroup("/")
            .WithTags("Drivers")
            .RequireAuthorization(AuthorizationPolicies.StaffManagement);

        settlement.MapGet("/locations/{locationId:guid}/shifts", (Guid locationId, DriverShiftStatus? status, DriverShiftService service, CancellationToken ct) =>
            service.ListAsync(locationId, status, ct));

        settlement.MapPost("/shifts/{id:guid}/settle", (Guid id, SettleShiftRequest request, DriverShiftService service, CancellationToken ct) =>
            service.SettleAsync(id, request, ct));

        // Live map for front of house (the service excludes kitchen and drivers).
        routes.MapGet("/locations/{locationId:guid}/drivers/positions", (Guid locationId, DriverTrackingService service, CancellationToken ct) =>
                service.ListLatestAsync(locationId, ct))
            .WithTags("Drivers")
            .RequireAuthorization(AuthorizationPolicies.CompanyStaff);

        return routes;
    }
}
