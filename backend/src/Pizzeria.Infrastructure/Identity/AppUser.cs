using Microsoft.AspNetCore.Identity;

namespace Pizzeria.Infrastructure.Identity;

/// <summary>
/// Platform user. Staff belong to exactly one company; <see cref="CompanyId"/> is <c>null</c> only for PlatformAdmin.
/// </summary>
/// <remarks>
/// Users are deliberately NOT under the global tenant query filter: Identity has to find the user
/// during login, before any tenant is known. Every user query outside Identity must filter by
/// <see cref="CompanyId"/> explicitly (see <c>UserAccountService</c>).
/// </remarks>
public sealed class AppUser : IdentityUser<Guid>
{
    public Guid? CompanyId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public List<UserLocation> Locations { get; } = [];
}

public sealed class AppRole : IdentityRole<Guid>
{
    public AppRole()
    {
    }

    public AppRole(string name)
        : base(name)
    {
    }
}

/// <summary>Assignment of a location-scoped user (manager, staff, kitchen, driver) to a location.</summary>
public sealed class UserLocation
{
    public Guid UserId { get; set; }

    public Guid LocationId { get; set; }
}
