using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pizzeria.Application.Customers;
using Pizzeria.Application.Errors;
using Pizzeria.Domain.Identity;
using Pizzeria.Infrastructure.Persistence;

namespace Pizzeria.Infrastructure.Identity;

public sealed class CustomerIdentityService(UserManager<AppUser> userManager, AppDbContext db, TimeProvider clock) : ICustomerIdentityService
{
    public async Task<Guid> CreateAsync(Guid companyId, string email, string password, string name, string? phone, CancellationToken ct)
    {
        var user = new AppUser
        {
            Id = Guid.CreateVersion7(),
            UserName = CustomerUserNames.For(companyId, email),
            Email = email,
            // Not confirmed: sending e-mails is not implemented yet (CLAUDE.md, open decisions).
            EmailConfirmed = false,
            PhoneNumber = phone,
            CompanyId = companyId,
            DisplayName = name,
            CreatedAt = clock.GetUtcNow(),
        };

        IdentityErrors.ThrowIfFailed(await userManager.CreateAsync(user, password).ConfigureAwait(false));
        IdentityErrors.ThrowIfFailed(await userManager.AddToRoleAsync(user, Roles.Customer).ConfigureAwait(false));
        return user.Id;
    }

    public async Task<CustomerProfileDto?> FindAsync(Guid companyId, Guid customerId, CancellationToken ct)
    {
        var user = await FindCustomerAsync(companyId, customerId, ct).ConfigureAwait(false);
        return user is null ? null : new CustomerProfileDto(user.Email ?? string.Empty, user.DisplayName, user.PhoneNumber);
    }

    public async Task UpdateAsync(Guid companyId, Guid customerId, string name, string? phone, CancellationToken ct)
    {
        var user = await FindCustomerAsync(companyId, customerId, ct).ConfigureAwait(false)
            ?? throw new NotFoundException(ErrorCodes.NotFound);
        user.DisplayName = name;
        user.PhoneNumber = phone;
        IdentityErrors.ThrowIfFailed(await userManager.UpdateAsync(user).ConfigureAwait(false));
    }

    public async Task DeleteAsync(Guid companyId, Guid customerId, CancellationToken ct)
    {
        var user = await FindCustomerAsync(companyId, customerId, ct).ConfigureAwait(false)
            ?? throw new NotFoundException(ErrorCodes.NotFound);

        // Orders keep their customer snapshot; the database sets their customer_id to NULL.
        IdentityErrors.ThrowIfFailed(await userManager.DeleteAsync(user).ConfigureAwait(false));
    }

    private Task<AppUser?> FindCustomerAsync(Guid companyId, Guid customerId, CancellationToken ct) =>
        // Users have no tenant filter: company and role are checked explicitly.
        db.Users
            .Where(u => u.Id == customerId && u.CompanyId == companyId)
            .Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Customer)))
            .FirstOrDefaultAsync(ct);
}
