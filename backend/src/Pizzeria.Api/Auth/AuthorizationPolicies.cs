using Microsoft.AspNetCore.Authorization;
using Pizzeria.Domain.Identity;

namespace Pizzeria.Api.Auth;

/// <summary>
/// Coarse role gates on endpoints. Fine-grained checks (tenant, location access) are always
/// repeated in the Application layer — an endpoint policy alone is never the only guard.
/// </summary>
public static class AuthorizationPolicies
{
    public const string PlatformAdmin = nameof(PlatformAdmin);
    public const string CompanyAdmin = nameof(CompanyAdmin);
    public const string StaffManagement = nameof(StaffManagement);
    public const string CompanyStaff = nameof(CompanyStaff);
    public const string Driver = nameof(Driver);
    public const string Agent = nameof(Agent);
    public const string Customer = nameof(Customer);

    public static void Configure(AuthorizationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Secure by default: every endpoint requires a signed-in user unless it opts out with AllowAnonymous.
        builder.SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        builder.AddPolicy(PlatformAdmin, p => p.RequireRole(Roles.PlatformAdmin));
        builder.AddPolicy(CompanyAdmin, p => p.RequireRole(Roles.CompanyAdmin));
        builder.AddPolicy(Driver, p => p.RequireRole(Roles.Driver));
        builder.AddPolicy(Customer, p => p.RequireRole(Roles.Customer));

        // The only policy that accepts agent device tokens (agent hub).
        builder.AddPolicy(Agent, p => p.AddAuthenticationSchemes(AgentTokenAuthenticationHandler.SchemeName).RequireRole(Roles.Agent));
        builder.AddPolicy(StaffManagement, p => p.RequireRole(Roles.CompanyAdmin, Roles.LocationManager));
        builder.AddPolicy(
            CompanyStaff,
            p => p.RequireRole(Roles.CompanyAdmin, Roles.LocationManager, Roles.Staff, Roles.Kitchen, Roles.Driver));
    }
}
