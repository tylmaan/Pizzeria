using System.Text;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pizzeria.Application.Errors;
using Pizzeria.Application.Users;
using Pizzeria.Infrastructure.Persistence;

namespace Pizzeria.Infrastructure.Identity;

public sealed class UserAccountService(UserManager<AppUser> userManager, AppDbContext db, TimeProvider clock)
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
        ThrowIfFailed(created);

        var roleAdded = await userManager.AddToRoleAsync(user, account.Role).ConfigureAwait(false);
        ThrowIfFailed(roleAdded);

        return user.Id;
    }

    public async Task<IReadOnlyList<UserDto>> ListAsync(
        Guid companyId,
        IReadOnlyCollection<Guid>? locationIds,
        CancellationToken ct)
    {
        // Users are not under the global tenant filter (see AppUser), so filter by company explicitly.
        var users = db.Users.AsNoTracking().Where(u => u.CompanyId == companyId);
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

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        if (result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateUserName)
                                         or nameof(IdentityErrorDescriber.DuplicateEmail)))
        {
            throw new ConflictException(ErrorCodes.UserEmailTaken);
        }

        var failures = result.Errors.Select(e => new ValidationFailure(
            e.Code.StartsWith("Password", StringComparison.Ordinal) ? "Password" : "Email",
            e.Description)
        {
            ErrorCode = "validation." + ToSnakeCase(e.Code),
        });
        throw new ValidationException(failures);
    }

    private static string ToSnakeCase(string value)
    {
        var builder = new StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsUpper(value[i]) && i > 0)
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(value[i]));
        }

        return builder.ToString();
    }
}
