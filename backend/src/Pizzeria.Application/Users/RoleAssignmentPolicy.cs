using Pizzeria.Application.Tenancy;
using Pizzeria.Domain.Identity;

namespace Pizzeria.Application.Users;

/// <summary>Which roles a user may grant to the staff accounts they create.</summary>
public static class RoleAssignmentPolicy
{
    private static readonly HashSet<string> CompanyAdminAssignable =
        [Roles.CompanyAdmin, Roles.LocationManager, Roles.Staff, Roles.Kitchen, Roles.Driver];

    private static readonly HashSet<string> LocationManagerAssignable = [Roles.Staff, Roles.Kitchen, Roles.Driver];

    public static bool CanAssign(ITenantContext actor, string role)
    {
        ArgumentNullException.ThrowIfNull(actor);

        if (actor.CompanyId is null)
        {
            return false;
        }

        if (actor.IsCompanyAdmin)
        {
            return CompanyAdminAssignable.Contains(role);
        }

        return actor.IsInRole(Roles.LocationManager) && LocationManagerAssignable.Contains(role);
    }
}
