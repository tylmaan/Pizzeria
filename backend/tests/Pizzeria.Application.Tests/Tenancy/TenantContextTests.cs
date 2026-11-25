using Pizzeria.Application.Tenancy;
using Pizzeria.Domain.Identity;

namespace Pizzeria.Application.Tests.Tenancy;

public sealed class TenantContextTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid AssignedLocation = Guid.NewGuid();
    private static readonly Guid OtherLocation = Guid.NewGuid();

    [Fact]
    public void CompanyAdmin_CanAccessEveryLocationOfCompany()
    {
        var tenant = User(Roles.CompanyAdmin);

        Assert.True(tenant.IsCompanyAdmin);
        Assert.True(tenant.CanAccessLocation(OtherLocation));
    }

    [Theory]
    [InlineData(Roles.LocationManager)]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.Kitchen)]
    [InlineData(Roles.Driver)]
    public void LocationScopedUser_CanAccessOnlyAssignedLocations(string role)
    {
        var tenant = User(role, AssignedLocation);

        Assert.True(tenant.CanAccessLocation(AssignedLocation));
        Assert.False(tenant.CanAccessLocation(OtherLocation));
    }

    [Fact]
    public void PlatformAdmin_IsNotCompanyAdminAndHasNoLocationAccess()
    {
        var tenant = new TenantContext();
        tenant.SetUser(Guid.NewGuid(), companyId: null, [Roles.PlatformAdmin], []);

        Assert.True(tenant.IsPlatformAdmin);
        Assert.False(tenant.IsCompanyAdmin);
        Assert.False(tenant.CanAccessLocation(AssignedLocation));
    }

    [Fact]
    public void CompanyAdminRoleWithoutCompany_IsNotCompanyAdmin()
    {
        var tenant = new TenantContext();
        tenant.SetUser(Guid.NewGuid(), companyId: null, [Roles.CompanyAdmin], []);

        Assert.False(tenant.IsCompanyAdmin);
        Assert.False(tenant.CanAccessLocation(AssignedLocation));
    }

    [Fact]
    public void Anonymous_HasTenantButNoUserOrLocationAccess()
    {
        var tenant = new TenantContext();
        tenant.SetAnonymous(CompanyId);

        Assert.Equal(CompanyId, tenant.CompanyId);
        Assert.Null(tenant.UserId);
        Assert.False(tenant.CanAccessLocation(AssignedLocation));
    }

    [Fact]
    public void Unresolved_HasNoTenant()
    {
        var tenant = new TenantContext();

        Assert.Null(tenant.CompanyId);
        Assert.False(tenant.CanAccessLocation(AssignedLocation));
    }

    private static TenantContext User(string role, params Guid[] locations)
    {
        var tenant = new TenantContext();
        tenant.SetUser(Guid.NewGuid(), CompanyId, [role], locations);
        return tenant;
    }
}
