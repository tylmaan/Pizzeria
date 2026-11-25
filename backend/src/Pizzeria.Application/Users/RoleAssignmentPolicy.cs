using Pizzeria.Application.Tenancy;
using Pizzeria.Domain.Identity;

namespace Pizzeria.Application.Users;

/// <summary>Which roles a user may grant to the staff accounts they create.</summary>
public static class RoleAssignmentPolicy
{
    private static readonly string[] CompanyAdminAssignable =
        [Roles.CompanyAdmin, Roles.LocationManager, Roles.Staff, Roles.Kitchen, Roles.Driver];

    private static readonly string[] LocationManagerAssignable = [Roles.Staff, Roles.Kitchen, Roles.Driver];

    public static IReadOnlyList<string> AssignableRoles(ITenantContext actor)
    {
        ArgumentNullException.ThrowIfNull(actor);

        if (actor.CompanyId is null)
        {
            return [];
        }

        if (actor.IsCompanyAdmin)
        {
            return CompanyAdminAssignable;
        }

        return actor.IsInRole(Roles.LocationManager) ? LocationManagerAssignable : [];
    }

    public static bool CanAssign(ITenantContext actor, string role) => AssignableRoles(actor).Contains(role);
}
