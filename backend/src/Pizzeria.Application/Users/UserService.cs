using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pizzeria.Application.Abstractions;
using Pizzeria.Application.Errors;
using Pizzeria.Application.Tenancy;
using Pizzeria.Domain.Identity;

namespace Pizzeria.Application.Users;

public sealed class UserService(
    IAppDbContext db,
    ITenantContext tenant,
    IUserAccountService accounts,
    IValidator<CreateUserRequest> validator)
{
    /// <summary>Company admins see all staff of the company, location managers the staff of their locations.</summary>
    public Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct)
    {
        var companyId = RequireStaffManager();
        return accounts.ListAsync(companyId, tenant.IsCompanyAdmin ? null : tenant.LocationIds.ToList(), ct);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        var companyId = RequireStaffManager();
        await validator.ValidateAndThrowAsync(request, ct).ConfigureAwait(false);

        if (!RoleAssignmentPolicy.CanAssign(tenant, request.Role))
        {
            throw new ForbiddenException(ErrorCodes.RoleNotAssignable);
        }

        var locationIds = (request.LocationIds ?? []).Distinct().ToList();
        var isLocationScoped = Roles.LocationScoped.Contains(request.Role);
        if (isLocationScoped && locationIds.Count == 0)
        {
            throw new ConflictException(ErrorCodes.LocationsRequired);
        }

        if (!isLocationScoped && locationIds.Count > 0)
        {
            throw new ConflictException(ErrorCodes.LocationsNotAllowed);
        }

        await EnsureLocationsAccessibleAsync(locationIds, ct).ConfigureAwait(false);

        var account = new NewUserAccount(
            companyId,
            request.Email.Trim(),
            request.Password,
            request.DisplayName.Trim(),
            request.Role,
            locationIds);
        var id = await accounts.CreateAsync(account, ct).ConfigureAwait(false);

        return new UserDto(id, account.Email, account.DisplayName, account.Role, locationIds);
    }

    private async Task EnsureLocationsAccessibleAsync(List<Guid> locationIds, CancellationToken ct)
    {
        if (locationIds.Count == 0)
        {
            return;
        }

        // Locations of other companies are invisible through the tenant filter, so they count as missing.
        var existing = await db.Locations.CountAsync(l => locationIds.Contains(l.Id), ct).ConfigureAwait(false);
        if (existing != locationIds.Count || !locationIds.All(tenant.CanAccessLocation))
        {
            throw new NotFoundException(ErrorCodes.LocationNotFound);
        }
    }

    private Guid RequireStaffManager()
    {
        if (tenant.CompanyId is not { } companyId || !(tenant.IsCompanyAdmin || tenant.IsInRole(Roles.LocationManager)))
        {
            throw new ForbiddenException();
        }

        return companyId;
    }
}
