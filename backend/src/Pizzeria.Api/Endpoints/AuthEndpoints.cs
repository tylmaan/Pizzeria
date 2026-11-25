using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Pizzeria.Application.Errors;
using Pizzeria.Application.Tenancy;
using Pizzeria.Infrastructure.Identity;

namespace Pizzeria.Api.Endpoints;

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record MeResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    Guid? CompanyId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<Guid> LocationIds);

public static class AuthEndpoints
{
    public const string LoginRateLimitPolicy = "login";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting(LoginRateLimitPolicy)
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous()
            .Produces<AccessTokenResponse>();

        group.MapGet("/me", GetMe);

        return routes;
    }

    /// <summary>Signs in with the Identity bearer token scheme; the handler writes the token response.</summary>
    private static async Task<Results<EmptyHttpResult, ProblemHttpResult>> LoginAsync(
        LoginRequest request,
        SignInManager<AppUser> signInManager)
    {
        signInManager.AuthenticationScheme = IdentityConstants.BearerScheme;
        var result = await signInManager
            .PasswordSignInAsync(request.Email, request.Password, isPersistent: false, lockoutOnFailure: true)
            .ConfigureAwait(false);

        if (result.Succeeded)
        {
            return TypedResults.Empty;
        }

        return TypedResults.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = result.IsLockedOut ? ErrorCodes.LockedOut : ErrorCodes.InvalidCredentials,
            });
    }

    /// <summary>
    /// Issues a new token pair. Claims are rebuilt from the database, so role and location changes apply;
    /// a changed security stamp (password reset, role change) invalidates the refresh token.
    /// </summary>
    private static async Task<Results<SignInHttpResult, ChallengeHttpResult>> RefreshAsync(
        RefreshRequest request,
        SignInManager<AppUser> signInManager,
        IOptionsMonitor<BearerTokenOptions> bearerOptions,
        TimeProvider clock)
    {
        var protector = bearerOptions.Get(IdentityConstants.BearerScheme).RefreshTokenProtector;
        var ticket = protector.Unprotect(request.RefreshToken);

        if (ticket?.Properties.ExpiresUtc is not { } expiresUtc
            || clock.GetUtcNow() >= expiresUtc
            || await signInManager.ValidateSecurityStampAsync(ticket.Principal).ConfigureAwait(false) is not { } user)
        {
            return TypedResults.Challenge();
        }

        var principal = await signInManager.CreateUserPrincipalAsync(user).ConfigureAwait(false);
        return TypedResults.SignIn(principal, authenticationScheme: IdentityConstants.BearerScheme);
    }

    private static MeResponse GetMe(ClaimsPrincipal user, ITenantContext tenant) =>
        new(
            tenant.UserId!.Value,
            user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
            user.FindFirstValue(TenantClaims.DisplayName) ?? string.Empty,
            tenant.CompanyId,
            [.. tenant.Roles],
            [.. tenant.LocationIds]);
}
