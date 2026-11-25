using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pizzeria.Application.Abstractions;
using Pizzeria.Application.Delivery;
using Pizzeria.Application.Errors;
using Pizzeria.Application.Tenancy;
using Pizzeria.Domain.Tenancy;

namespace Pizzeria.Application.Locations;

public sealed class LocationService(
    IAppDbContext db,
    ITenantContext tenant,
    TimeProvider clock,
    IGeocodingService geocoding,
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

        await LocateAsync(location, ct).ConfigureAwait(false);
        db.Locations.Add(location);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return LocationDto.From(location);
    }

    public async Task<LocationDto> UpdateAsync(Guid id, UpdateLocationRequest request, CancellationToken ct)
    {
        RequireCompanyAdmin();
        await updateValidator.ValidateAndThrowAsync(request, ct).ConfigureAwait(false);

        var location = await FindAccessibleAsync(id, ct).ConfigureAwait(false);
        var previousAddress = location.Address;
        location.Update(request.Name, request.Address.ToDomain(), request.TimeZoneId);
        if (location.Address != previousAddress || location.Position is null)
        {
            await LocateAsync(location, ct).ConfigureAwait(false);
        }

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

    /// <summary>Best effort: a location whose address cannot be geocoded simply has no position.</summary>
    private async Task LocateAsync(Location location, CancellationToken ct)
    {
        var address = location.Address;
        var found = await geocoding.GeocodeAsync(new AddressQuery(address.Street, address.PostalCode, address.City), ct)
            .ConfigureAwait(false);
        location.SetPosition(found?.Point);
    }

    private void RequireCompanyAdmin()
    {
        if (!tenant.IsCompanyAdmin)
        {
            throw new ForbiddenException();
        }
    }
}
