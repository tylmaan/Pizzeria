using Pizzeria.Domain.Identity;

namespace Pizzeria.Application.Tenancy;

/// <summary>Scoped, settable implementation of <see cref="ITenantContext"/>.</summary>
public sealed class TenantContext : ITenantContext
{
    private static readonly IReadOnlySet<string> NoRoles = new HashSet<string>();
    private static readonly IReadOnlySet<Guid> NoLocations = new HashSet<Guid>();

    public Guid? UserId { get; private set; }

    public Guid? CompanyId { get; private set; }

    public IReadOnlySet<string> Roles { get; private set; } = NoRoles;

    public IReadOnlySet<Guid> LocationIds { get; private set; } = NoLocations;

    public bool IsPlatformAdmin => IsInRole(Domain.Identity.Roles.PlatformAdmin);

    public bool IsCompanyAdmin => CompanyId is not null && IsInRole(Domain.Identity.Roles.CompanyAdmin);

    public bool IsInRole(string role) => Roles.Contains(role);

    public bool CanAccessLocation(Guid locationId) =>
        CompanyId is not null && (IsCompanyAdmin || LocationIds.Contains(locationId));

    public void SetUser(Guid userId, Guid? companyId, IEnumerable<string> roles, IEnumerable<Guid> locationIds)
    {
        UserId = userId;
        CompanyId = companyId;
        Roles = roles.ToHashSet(StringComparer.Ordinal);
        LocationIds = locationIds.ToHashSet();
    }

    /// <summary>Anonymous request in the context of a company (customer shop resolved from host name).</summary>
    public void SetAnonymous(Guid companyId)
    {
        UserId = null;
        CompanyId = companyId;
        Roles = NoRoles;
        LocationIds = NoLocations;
    }

    /// <summary>
    /// Non-HTTP work (seeding, background jobs) acting as an administrator of the given company.
    /// </summary>
    public void SetSystemForCompany(Guid companyId)
    {
        UserId = null;
        CompanyId = companyId;
        Roles = new HashSet<string> { Domain.Identity.Roles.CompanyAdmin };
        LocationIds = NoLocations;
    }
}
