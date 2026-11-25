using Pizzeria.Domain.Common;
using Pizzeria.Domain.Tenancy;

namespace Pizzeria.Domain.Tests.Tenancy;

public sealed class LocationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Address SomeAddress = new("ul. Lipowa 1", "15-001", "Białystok");

    [Fact]
    public void Create_WithoutTimeZone_DefaultsToWarsawAndIsActive()
    {
        var location = Location.Create(CompanyId, "Centrum", SomeAddress, null, Now);

        Assert.Equal("Europe/Warsaw", location.TimeZoneId);
        Assert.True(location.IsActive);
        Assert.Equal(CompanyId, location.CompanyId);
    }

    [Fact]
    public void Create_UnknownTimeZone_Throws()
    {
        var ex = Assert.Throws<DomainException>(() => Location.Create(CompanyId, "Centrum", SomeAddress, "Mars/Olympus", Now));

        Assert.Equal(TenancyErrors.InvalidTimeZone, ex.Code);
    }

    [Fact]
    public void Create_WithoutCompany_Throws()
    {
        Assert.Throws<ArgumentException>(() => Location.Create(Guid.Empty, "Centrum", SomeAddress, null, Now));
    }

    [Fact]
    public void Update_TrimsNameAndAddress()
    {
        var location = Location.Create(CompanyId, "Centrum", SomeAddress, null, Now);

        location.Update(" Rynek ", new Address(" Rynek 5 ", " 00-001 ", " Warszawa "), "Europe/Warsaw");

        Assert.Equal("Rynek", location.Name);
        Assert.Equal(new Address("Rynek 5", "00-001", "Warszawa"), location.Address);
    }

    [Fact]
    public void Update_BlankName_Throws()
    {
        var location = Location.Create(CompanyId, "Centrum", SomeAddress, null, Now);

        var ex = Assert.Throws<DomainException>(() => location.Update(" ", SomeAddress, "Europe/Warsaw"));

        Assert.Equal(TenancyErrors.InvalidName, ex.Code);
    }

    [Fact]
    public void DeactivateAndActivate_ToggleAvailability()
    {
        var location = Location.Create(CompanyId, "Centrum", SomeAddress, null, Now);

        location.Deactivate();
        Assert.False(location.IsActive);

        location.Activate();
        Assert.True(location.IsActive);
    }
}
