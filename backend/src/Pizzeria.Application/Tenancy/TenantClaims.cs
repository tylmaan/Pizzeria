namespace Pizzeria.Application.Tenancy;

/// <summary>Custom claim types carried in the access token.</summary>
public static class TenantClaims
{
    public const string CompanyId = "company_id";
    public const string LocationId = "location_id";
    public const string DisplayName = "display_name";
}
