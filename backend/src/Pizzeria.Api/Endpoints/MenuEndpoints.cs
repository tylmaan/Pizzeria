using Pizzeria.Api.Auth;
using Pizzeria.Application.Menu;

namespace Pizzeria.Api.Endpoints;

public static class MenuEndpoints
{
    public static IEndpointRouteBuilder MapMenuEndpoints(this IEndpointRouteBuilder routes)
    {
        // Staff read the menu (phone orders); only company admins edit it.
        var group = routes.MapGroup("/menu")
            .WithTags("Menu")
            .RequireAuthorization(AuthorizationPolicies.CompanyStaff);

        group.MapGet("/", (MenuService service, CancellationToken ct) => service.GetAsync(ct));

        var edit = group.MapGroup("/").RequireAuthorization(AuthorizationPolicies.CompanyAdmin);

        edit.MapPut("/settings", (MenuSettingsDto request, MenuService service, CancellationToken ct) =>
            service.UpdateSettingsAsync(request, ct));

        edit.MapPost("/categories", async (SaveCategoryRequest request, MenuService service, CancellationToken ct) =>
        {
            var category = await service.CreateCategoryAsync(request, ct).ConfigureAwait(false);
            return TypedResults.Created($"/api/menu/categories/{category.Id}", category);
        });
        edit.MapPut("/categories/{id:guid}", (Guid id, SaveCategoryRequest request, MenuService service, CancellationToken ct) =>
            service.UpdateCategoryAsync(id, request, ct));

        edit.MapPost("/products", async (SaveProductRequest request, MenuService service, CancellationToken ct) =>
        {
            var product = await service.CreateProductAsync(request, ct).ConfigureAwait(false);
            return TypedResults.Created($"/api/menu/products/{product.Id}", product);
        });
        edit.MapPut("/products/{id:guid}", (Guid id, SaveProductRequest request, MenuService service, CancellationToken ct) =>
            service.UpdateProductAsync(id, request, ct));

        edit.MapPost("/modifier-groups", async (SaveModifierGroupRequest request, MenuService service, CancellationToken ct) =>
        {
            var modifierGroup = await service.CreateModifierGroupAsync(request, ct).ConfigureAwait(false);
            return TypedResults.Created($"/api/menu/modifier-groups/{modifierGroup.Id}", modifierGroup);
        });
        edit.MapPut("/modifier-groups/{id:guid}", (Guid id, SaveModifierGroupRequest request, MenuService service, CancellationToken ct) =>
            service.UpdateModifierGroupAsync(id, request, ct));

        return routes;
    }
}
