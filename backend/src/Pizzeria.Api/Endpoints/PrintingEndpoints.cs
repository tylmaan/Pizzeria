using Pizzeria.Api.Auth;
using Pizzeria.Application.Printing;

namespace Pizzeria.Api.Endpoints;

public static class PrintingEndpoints
{
    public static IEndpointRouteBuilder MapPrintingEndpoints(this IEndpointRouteBuilder routes)
    {
        // Agents and print settings: company admins and location managers (checked again in PrintingService).
        var manage = routes.MapGroup("/")
            .WithTags("Printing")
            .RequireAuthorization(AuthorizationPolicies.StaffManagement);

        manage.MapPost("/locations/{locationId:guid}/print-agents", async (Guid locationId, RegisterAgentRequest request, PrintingService service, CancellationToken ct) =>
        {
            var registered = await service.RegisterAgentAsync(locationId, request, ct).ConfigureAwait(false);
            return TypedResults.Created($"/api/locations/{locationId}/print-agents", registered);
        });

        manage.MapPost("/print-agents/{id:guid}/revoke", async (Guid id, PrintingService service, CancellationToken ct) =>
        {
            await service.RevokeAgentAsync(id, ct).ConfigureAwait(false);
            return TypedResults.NoContent();
        });

        manage.MapPut("/locations/{locationId:guid}/print-settings", (Guid locationId, PrintSettingsDto request, PrintingService service, CancellationToken ct) =>
            service.UpdateSettingsAsync(locationId, request, ct));

        // Status and jobs: front of house too (retry, reprint).
        var staff = routes.MapGroup("/")
            .WithTags("Printing")
            .RequireAuthorization(AuthorizationPolicies.CompanyStaff);

        staff.MapGet("/locations/{locationId:guid}/print-agents", (Guid locationId, PrintingService service, CancellationToken ct) =>
            service.ListAgentsAsync(locationId, ct));

        staff.MapGet("/locations/{locationId:guid}/print-settings", (Guid locationId, PrintingService service, CancellationToken ct) =>
            service.GetSettingsAsync(locationId, ct));

        staff.MapGet("/locations/{locationId:guid}/print-jobs", (Guid locationId, PrintingService service, CancellationToken ct) =>
            service.ListJobsAsync(locationId, ct));

        staff.MapPost("/print-jobs/{id:guid}/retry", (Guid id, PrintingService service, CancellationToken ct) =>
            service.RetryJobAsync(id, ct));

        staff.MapPost("/orders/{orderId:guid}/reprint", (Guid orderId, PrintingService service, CancellationToken ct) =>
            service.ReprintTicketAsync(orderId, ct));

        return routes;
    }
}
