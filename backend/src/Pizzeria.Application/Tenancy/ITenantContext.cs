namespace Pizzeria.Application.Tenancy;

/// <summary>
/// Who is calling and in which company (tenant). Populated per request by the API
/// (from the access token, or from the shop host name for anonymous customers).
/// </summary>
public interface ITenantContext
{
    Guid? UserId { get; }

    /// <summary>Current tenant. <c>null</c> = no tenant: tenant-filtered queries return nothing.</summary>
    Guid? CompanyId { get; }

    IReadOnlySet<string> Roles { get; }

    /// <summary>Locations the user is assigned to (location-scoped roles only).</summary>
    IReadOnlySet<Guid> LocationIds { get; }

    bool IsPlatformAdmin { get; }

    bool IsCompanyAdmin { get; }

    bool IsInRole(string role);

    /// <summary>
    /// Whether the user may work with the given location. A company admin may access every location
    /// of their company — callers must load the location through the tenant-filtered context,
    /// so a location of another company is never found in the first place.
    /// </summary>
    bool CanAccessLocation(Guid locationId);
}
