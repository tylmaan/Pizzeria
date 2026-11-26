using Pizzeria.Api.Auth;
using Pizzeria.Application.Reports;

namespace Pizzeria.Api.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder routes)
    {
        // Company admins (any location or all) and location managers (own location); checked in ReportService.
        routes.MapGet("/reports", (Guid? locationId, DateOnly from, DateOnly to, ReportService service, CancellationToken ct) =>
                service.GetAsync(locationId, from, to, ct))
            .WithTags("Reports")
            .RequireAuthorization(AuthorizationPolicies.StaffManagement);

        return routes;
    }
}
