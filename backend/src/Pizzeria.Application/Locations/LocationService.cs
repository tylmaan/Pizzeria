using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pizzeria.Application.Abstractions;
using Pizzeria.Application.Errors;
using Pizzeria.Application.Tenancy;
using Pizzeria.Domain.Tenancy;

namespace Pizzeria.Application.Locations;

public sealed class LocationService(
    IAppDbContext db,
    ITenantContext tenant,
    TimeProvider clock,
    IValidator<CreateLocationRequest> createValidator,
    IValidator<UpdateLocationRequest> updateValidator)
{
    /// <summary>Company admins see all locations of the company, other staff only their own.</summary>
    public async Task<IReadOnlyList<LocationDto>> ListAsync(CancellationToken ct)
    {
        var query = db.Locations.AsNoTracking();
        if (!tenant.IsCompanyAdmin)
        {
            var allowed = tenant.LocationIds.ToList();
            query = query.Where(l => allowed.Contains(l.Id));
        }

        var locations = await query.OrderBy(l => l.Name).ToListAsync(ct).ConfigureAwait(false);
        return locations.ConvertAll(LocationDto.From);
    }

    public async Task<LocationDto> GetAsync(Guid id, CancellationToken ct)
    {
        var location = await FindAccessibleAsync(id, ct).ConfigureAwait(false);
        return LocationDto.From(location);
    }

    public async Task<LocationDto> CreateAsync(CreateLocationRequest request, CancellationToken ct)
    {
        RequireCompanyAdmin();
        await createValidator.ValidateAndThrowAsync(request, ct).ConfigureAwait(false);

        var location = Location.Create(
            tenant.CompanyId!.Value,
            request.Name,
            request.Address.ToDomain(),
            request.TimeZoneId,
            clock.GetUtcNow());

        db.Locations.Add(location);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return LocationDto.From(location);
    }

    public async Task<LocationDto> UpdateAsync(Guid id, UpdateLocationRequest request, CancellationToken ct)
    {
        RequireCompanyAdmin();
        await updateValidator.ValidateAndThrowAsync(request, ct).ConfigureAwait(false);

        var location = await FindAccessibleAsync(id, ct).ConfigureAwait(false);
        location.Update(request.Name, request.Address.ToDomain(), request.TimeZoneId);
        if (request.IsActive)
        {
            location.Activate();
        }
        else
        {
            location.Deactivate();
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return LocationDto.From(location);
    }

    private async Task<Location> FindAccessibleAsync(Guid id, CancellationToken ct)
    {
        // The tenant filter hides locations of other companies; the access check hides
        // locations of the same company that the user is not assigned to.
        if (!tenant.CanAccessLocation(id))
        {
            throw new NotFoundException(ErrorCodes.LocationNotFound);
        }

        return await db.Locations.FirstOrDefaultAsync(l => l.Id == id, ct).ConfigureAwait(false)
            ?? throw new NotFoundException(ErrorCodes.LocationNotFound);
    }

    private void RequireCompanyAdmin()
    {
        if (!tenant.IsCompanyAdmin)
        {
            throw new ForbiddenException();
        }
    }
}
