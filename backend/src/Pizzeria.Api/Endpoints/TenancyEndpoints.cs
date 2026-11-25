using Pizzeria.Api.Auth;
using Pizzeria.Application.Locations;
using Pizzeria.Application.Platform;
using Pizzeria.Application.Users;

namespace Pizzeria.Api.Endpoints;

public static class TenancyEndpoints
{
    public static IEndpointRouteBuilder MapPlatformEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/platform/companies")
            .WithTags("Platform")
            .RequireAuthorization(AuthorizationPolicies.PlatformAdmin);

        group.MapGet("/", (CompanyService service, CancellationToken ct) => service.ListAsync(ct));

        group.MapPost("/", async (CreateCompanyRequest request, CompanyService service, CancellationToken ct) =>
        {
            var company = await service.CreateAsync(request, ct).ConfigureAwait(false);
            return TypedResults.Created($"/api/platform/companies/{company.Id}", company);
        });

        return routes;
    }

    public static IEndpointRouteBuilder MapLocationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/locations")
            .WithTags("Locations")
            .RequireAuthorization(AuthorizationPolicies.CompanyStaff);

        group.MapGet("/", (LocationService service, CancellationToken ct) => service.ListAsync(ct));

        group.MapGet("/{id:guid}", (Guid id, LocationService service, CancellationToken ct) => service.GetAsync(id, ct));

        group.MapPost("/", async (CreateLocationRequest request, LocationService service, CancellationToken ct) =>
        {
            var location = await service.CreateAsync(request, ct).ConfigureAwait(false);
            return TypedResults.Created($"/api/locations/{location.Id}", location);
        }).RequireAuthorization(AuthorizationPolicies.CompanyAdmin);

        group.MapPut(
            "/{id:guid}",
            (Guid id, UpdateLocationRequest request, LocationService service, CancellationToken ct) =>
                service.UpdateAsync(id, request, ct))
            .RequireAuthorization(AuthorizationPolicies.CompanyAdmin);

        return routes;
    }

    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/users")
            .WithTags("Users")
            .RequireAuthorization(AuthorizationPolicies.StaffManagement);

        group.MapGet("/", (UserService service, CancellationToken ct) => service.ListAsync(ct));

        group.MapPost("/", async (CreateUserRequest request, UserService service, CancellationToken ct) =>
        {
            var user = await service.CreateAsync(request, ct).ConfigureAwait(false);
            return TypedResults.Created($"/api/users/{user.Id}", user);
        });

        return routes;
    }
}
