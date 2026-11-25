namespace Pizzeria.Domain.Identity;

public static class Roles
{
    /// <summary>Operator of the whole platform (us). Not bound to any company.</summary>
    public const string PlatformAdmin = nameof(PlatformAdmin);

    /// <summary>Whole company: menu, prices, locations, staff, aggregated reports.</summary>
    public const string CompanyAdmin = nameof(CompanyAdmin);

    /// <summary>One or more locations: orders, location staff, settlements, location reports.</summary>
    public const string LocationManager = nameof(LocationManager);

    public const string Staff = nameof(Staff);

    public const string Kitchen = nameof(Kitchen);

    public const string Driver = nameof(Driver);

    public const string Customer = nameof(Customer);

    public static readonly IReadOnlyList<string> All =
        [PlatformAdmin, CompanyAdmin, LocationManager, Staff, Kitchen, Driver, Customer];

    /// <summary>Roles that work at specific locations and therefore need location assignments.</summary>
    public static readonly IReadOnlyList<string> LocationScoped = [LocationManager, Staff, Kitchen, Driver];
}
