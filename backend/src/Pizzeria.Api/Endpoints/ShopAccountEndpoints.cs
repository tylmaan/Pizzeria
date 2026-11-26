using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Pizzeria.Api.Auth;
using Pizzeria.Application.Customers;
using Pizzeria.Application.Errors;
using Pizzeria.Application.Tenancy;
using Pizzeria.Infrastructure.Identity;

namespace Pizzeria.Api.Endpoints;

/// <summary>
/// Optional customer accounts in a shop. Accounts belong to the shop's company (from the host name);
/// tokens are the same bearer tokens as for staff and are refreshed with <c>/api/auth/refresh</c>.
/// </summary>
public static class ShopAccountEndpoints
{
    public static IEndpointRouteBuilder MapShopAccountEndpoints(this IEndpointRouteBuilder routes)
    {
        var account = routes.MapGroup("/shop/account").WithTags("Shop account");

        account.MapPost("/register", RegisterAsync)
            .AllowAnonymous()
            .RequireRateLimiting(AuthEndpoints.LoginRateLimitPolicy)
            .Produces<AccessTokenResponse>();

        account.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting(AuthEndpoints.LoginRateLimitPolicy)
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        var mine = account.MapGroup("/").RequireAuthorization(AuthorizationPolicies.Customer);

        mine.MapGet("/", (CustomerAccountService service, CancellationToken ct) => service.GetProfileAsync(ct));

        mine.MapPut("/", (UpdateCustomerProfileRequest request, CustomerAccountService service, CancellationToken ct) =>
            service.UpdateProfileAsync(request, ct));

        mine.MapDelete("/", async (CustomerAccountService service, CancellationToken ct) =>
        {
            await service.DeleteAccountAsync(ct).ConfigureAwait(false);
            return TypedResults.NoContent();
        });

        mine.MapGet("/orders", (CustomerAccountService service, CancellationToken ct) => service.ListOrdersAsync(ct));

        mine.MapGet("/addresses", (CustomerAccountService service, CancellationToken ct) => service.ListAddressesAsync(ct));

        mine.MapPost("/addresses", async (SaveAddressRequest request, CustomerAccountService service, CancellationToken ct) =>
        {
            var address = await service.AddAddressAsync(request, ct).ConfigureAwait(false);
            return TypedResults.Created($"/api/shop/account/addresses/{address.Id}", address);
        });

        mine.MapPut("/addresses/{id:guid}", (Guid id, SaveAddressRequest request, CustomerAccountService service, CancellationToken ct) =>
            service.UpdateAddressAsync(id, request, ct));

        mine.MapDelete("/addresses/{id:guid}", async (Guid id, CustomerAccountService service, CancellationToken ct) =>
        {
            await service.DeleteAddressAsync(id, ct).ConfigureAwait(false);
            return TypedResults.NoContent();
        });

        return routes;
    }

    /// <summary>Creates the account and signs the customer in (the handler writes the token response).</summary>
    private static async Task<EmptyHttpResult> RegisterAsync(
        RegisterCustomerRequest request,
        CustomerAccountService service,
        SignInManager<AppUser> signInManager,
        CancellationToken ct)
    {
        var id = await service.RegisterAsync(request, ct).ConfigureAwait(false);
        var user = await signInManager.UserManager.FindByIdAsync(id.ToString()).ConfigureAwait(false);

        signInManager.AuthenticationScheme = IdentityConstants.BearerScheme;
        await signInManager.SignInAsync(user!, isPersistent: false).ConfigureAwait(false);
        return TypedResults.Empty;
    }

    private static async Task<Results<EmptyHttpResult, ProblemHttpResult>> LoginAsync(
        CustomerLoginRequest request,
        ITenantContext tenant,
        SignInManager<AppUser> signInManager)
    {
        if (tenant.CompanyId is not { } companyId)
        {
            throw new NotFoundException(ErrorCodes.ShopNotFound);
        }

        signInManager.AuthenticationScheme = IdentityConstants.BearerScheme;
        var result = await signInManager
            .PasswordSignInAsync(CustomerUserNames.For(companyId, request.Email), request.Password, isPersistent: false, lockoutOnFailure: true)
            .ConfigureAwait(false);

        return result.Succeeded ? TypedResults.Empty : AuthEndpoints.LoginFailed(result);
    }
}
