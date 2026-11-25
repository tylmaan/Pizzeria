using Pizzeria.Application.Tenancy;
using Pizzeria.Application.Users;
using Pizzeria.Domain.Identity;

namespace Pizzeria.Application.Tests.Users;

public sealed class RoleAssignmentPolicyTests
{
    [Theory]
    // Company admin manages the whole staff, including other company admins.
    [InlineData(Roles.CompanyAdmin, Roles.CompanyAdmin, true)]
    [InlineData(Roles.CompanyAdmin, Roles.LocationManager, true)]
    [InlineData(Roles.CompanyAdmin, Roles.Staff, true)]
    [InlineData(Roles.CompanyAdmin, Roles.Kitchen, true)]
    [InlineData(Roles.CompanyAdmin, Roles.Driver, true)]
    [InlineData(Roles.CompanyAdmin, Roles.PlatformAdmin, false)]
    [InlineData(Roles.CompanyAdmin, Roles.Customer, false)]
    // Location manager hires operational staff only.
    [InlineData(Roles.LocationManager, Roles.Staff, true)]
    [InlineData(Roles.LocationManager, Roles.Kitchen, true)]
    [InlineData(Roles.LocationManager, Roles.Driver, true)]
    [InlineData(Roles.LocationManager, Roles.LocationManager, false)]
    [InlineData(Roles.LocationManager, Roles.CompanyAdmin, false)]
    // Everyone else cannot create accounts.
    [InlineData(Roles.Staff, Roles.Staff, false)]
    [InlineData(Roles.Kitchen, Roles.Staff, false)]
    [InlineData(Roles.Driver, Roles.Driver, false)]
    [InlineData(Roles.Customer, Roles.Staff, false)]
    public void CanAssign_FollowsRoleHierarchy(string actorRole, string targetRole, bool expected)
    {
        var actor = new TenantContext();
        actor.SetUser(Guid.NewGuid(), Guid.NewGuid(), [actorRole], []);

        Assert.Equal(expected, RoleAssignmentPolicy.CanAssign(actor, targetRole));
    }

    [Fact]
    public void AssignableRoles_LocationManager_ListsOperationalStaffOnly()
    {
        var manager = new TenantContext();
        manager.SetUser(Guid.NewGuid(), Guid.NewGuid(), [Roles.LocationManager], [Guid.NewGuid()]);

        Assert.Equal([Roles.Staff, Roles.Kitchen, Roles.Driver], RoleAssignmentPolicy.AssignableRoles(manager));
    }

    [Fact]
    public void CanAssign_WithoutCompany_IsAlwaysFalse()
    {
        var platformAdmin = new TenantContext();
        platformAdmin.SetUser(Guid.NewGuid(), companyId: null, [Roles.PlatformAdmin], []);

        Assert.All(Roles.All, role => Assert.False(RoleAssignmentPolicy.CanAssign(platformAdmin, role)));
    }
}
