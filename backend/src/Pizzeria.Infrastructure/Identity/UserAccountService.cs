using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pizzeria.Application.Errors;
using Pizzeria.Application.Tenancy;
using Pizzeria.Application.Users;
using Pizzeria.Domain.Identity;
using Pizzeria.Infrastructure.Persistence;

namespace Pizzeria.Infrastructure.Identity;

public sealed class UserAccountService(UserManager<AppUser> userManager, AppDbContext db, TimeProvider clock, ITenantContext tenant)
    : IUserAccountService
{
    public async Task<Guid> CreateAsync(NewUserAccount account, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(account);

        var user = new AppUser
        {
            Id = Guid.CreateVersion7(),
            UserName = account.Email,
            Email = account.Email,
            EmailConfirmed = true,
            CompanyId = account.CompanyId,
            DisplayName = account.DisplayName,
            CreatedAt = clock.GetUtcNow(),
        };
        user.Locations.AddRange(account.LocationIds.Select(id => new UserLocation { UserId = user.Id, LocationId = id }));

        var created = await userManager.CreateAsync(user, account.Password).ConfigureAwait(false);
        IdentityErrors.ThrowIfFailed(created);

        var roleAdded = await userManager.AddToRoleAsync(user, account.Role).ConfigureAwait(false);
        IdentityErrors.ThrowIfFailed(roleAdded);

        return user.Id;
    }

    public async Task<IReadOnlyList<UserDto>> ListAsync(
        Guid companyId,
        IReadOnlyCollection<Guid>? locationIds,
        CancellationToken ct)
    {
        // Users are not under the global tenant filter (see AppUser), so filter by company explicitly.
        // Customers of the company are users too, but never part of the staff list.
        var users = db.Users.AsNoTracking()
            .Where(u => u.CompanyId == companyId)
            .Where(u => !db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Customer)));
        if (locationIds is not null)
        {
            users = users.Where(u => u.Locations.Any(l => locationIds.Contains(l.LocationId)));
        }

        var rows = await users
            .OrderBy(u => u.DisplayName)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                Role = db.UserRoles
                    .Where(ur => ur.UserId == u.Id)
                    .Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name)
                    .FirstOrDefault(),
                LocationIds = u.Locations.Select(l => l.LocationId).ToList(),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return rows.ConvertAll(r => new UserDto(r.Id, r.Email ?? string.Empty, r.DisplayName, r.Role ?? string.Empty, r.LocationIds));
    }

    public Task<bool> IsDriverAtLocationAsync(Guid userId, Guid locationId, CancellationToken ct) =>
        // Users have no tenant filter: the company check is explicit (current tenant).
        db.Users
            .Where(u => u.Id == userId && u.CompanyId == tenant.CompanyId && u.Locations.Any(l => l.LocationId == locationId))
            .Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Driver)))
            .AnyAsync(ct);
}
