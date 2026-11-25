using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pizzeria.Application.Tenancy;
using Pizzeria.Infrastructure.Persistence;

namespace Pizzeria.Infrastructure.Identity;

/// <summary>Adds tenant claims (company, assigned locations) to the principal stored in the access token.</summary>
public sealed class AppUserClaimsPrincipalFactory(
    UserManager<AppUser> userManager,
    RoleManager<AppRole> roleManager,
    IOptions<IdentityOptions> options,
    AppDbContext db)
    : UserClaimsPrincipalFactory<AppUser, AppRole>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user).ConfigureAwait(false);

        identity.AddClaim(new Claim(TenantClaims.DisplayName, user.DisplayName));

        if (user.CompanyId is { } companyId)
        {
            identity.AddClaim(new Claim(TenantClaims.CompanyId, companyId.ToString()));
        }

        var locationIds = await db.UserLocations
            .Where(ul => ul.UserId == user.Id)
            .Select(ul => ul.LocationId)
            .ToListAsync()
            .ConfigureAwait(false);
        foreach (var locationId in locationIds)
        {
            identity.AddClaim(new Claim(TenantClaims.LocationId, locationId.ToString()));
        }

        return identity;
    }
}
