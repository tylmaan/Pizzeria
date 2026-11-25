using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pizzeria.Application.Abstractions;
using Pizzeria.Application.Errors;
using Pizzeria.Application.Tenancy;
using Pizzeria.Domain.Delivery;
using Pizzeria.Domain.Identity;
using Location = Pizzeria.Domain.Tenancy.Location;

namespace Pizzeria.Application.Delivery;

/// <summary>Delivery zones of a location. Company admins manage all locations, managers only their own.</summary>
public sealed class DeliveryZoneService(
    IAppDbContext db,
    ITenantContext tenant,
    TimeProvider clock,
    IValidator<SaveDeliveryZoneRequest> validator)
{
    public async Task<IReadOnlyList<DeliveryZoneDto>> ListAsync(Guid locationId, CancellationToken ct)
    {
        await FindManagedLocationAsync(locationId, ct).ConfigureAwait(false);
        var zones = await db.DeliveryZones.AsNoTracking()
            .Where(z => z.LocationId == locationId)
            .OrderBy(z => z.Name)
            .ToListAsync(ct).ConfigureAwait(false);
        return zones.ConvertAll(DeliveryZoneDto.From);
    }

    public async Task<DeliveryZoneDto> CreateAsync(Guid locationId, SaveDeliveryZoneRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct).ConfigureAwait(false);
        var location = await FindManagedLocationAsync(locationId, ct).ConfigureAwait(false);

        var zone = DeliveryZone.Create(
            location,
            request.Name,
            [.. request.Boundary.Select(p => p.ToDomain())],
            request.DeliveryFee,
            request.MinOrderValue,
            request.EstimatedMinutes,
            clock.GetUtcNow());
        if (!request.IsActive)
        {
            zone.Update(zone.Name, zone.Boundary, zone.DeliveryFee, zone.MinOrderValue, zone.EstimatedMinutes, isActive: false);
        }

        db.DeliveryZones.Add(zone);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return DeliveryZoneDto.From(zone);
    }

    public async Task<DeliveryZoneDto> UpdateAsync(Guid locationId, Guid id, SaveDeliveryZoneRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct).ConfigureAwait(false);
        await FindManagedLocationAsync(locationId, ct).ConfigureAwait(false);

        var zone = await db.DeliveryZones.FirstOrDefaultAsync(z => z.Id == id && z.LocationId == locationId, ct).ConfigureAwait(false)
            ?? throw new NotFoundException(ErrorCodes.DeliveryZoneNotFound);
        zone.Update(
            request.Name,
            [.. request.Boundary.Select(p => p.ToDomain())],
            request.DeliveryFee,
            request.MinOrderValue,
            request.EstimatedMinutes,
            request.IsActive);

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return DeliveryZoneDto.From(zone);
    }

    private async Task<Location> FindManagedLocationAsync(Guid locationId, CancellationToken ct)
    {
        var canManage = tenant.IsCompanyAdmin || tenant.IsInRole(Roles.LocationManager);
        if (!canManage || !tenant.CanAccessLocation(locationId))
        {
            throw new NotFoundException(ErrorCodes.LocationNotFound);
        }

        return await db.Locations.FirstOrDefaultAsync(l => l.Id == locationId, ct).ConfigureAwait(false)
            ?? throw new NotFoundException(ErrorCodes.LocationNotFound);
    }
}

/// <summary>Where an address lies and which zone (and location) delivers there, if any.</summary>
public sealed record ResolvedDelivery(GeocodedAddress Address, DeliveryZone? Zone, Location? Location);

/// <summary>Address → geocoded point → the delivering zone. Shared by the delivery check and order placement.</summary>
public sealed class DeliveryResolver(IAppDbContext db, IGeocodingService geocoding)
{
    /// <exception cref="UnprocessableException">The address cannot be geocoded.</exception>
    public async Task<ResolvedDelivery> ResolveAsync(AddressQuery query, CancellationToken ct)
    {
        var address = await geocoding.GeocodeAsync(query, ct).ConfigureAwait(false)
            ?? throw new UnprocessableException(ErrorCodes.AddressNotFound);

        var point = address.Point.ToPoint();
        var candidates = await db.DeliveryZones
            .Where(z => z.IsActive && z.Area.Covers(point))
            .Join(db.Locations.Where(l => l.IsActive), z => z.LocationId, l => l.Id, (z, l) => new { Zone = z, Location = l })
            .ToListAsync(ct).ConfigureAwait(false);

        var zone = DeliveryZone.PickFor(candidates.Select(c => c.Zone), address.Point);
        return new ResolvedDelivery(address, zone, zone is null ? null : candidates.First(c => c.Zone == zone).Location);
    }
}

/// <summary>Answers "do you deliver to this address, from which location and on what terms".</summary>
public sealed class DeliveryCheckService(
    ITenantContext tenant,
    DeliveryResolver resolver,
    IValidator<DeliveryCheckRequest> validator)
{
    public async Task<DeliveryQuoteDto> CheckAsync(DeliveryCheckRequest request, CancellationToken ct)
    {
        // Anonymous shop requests get their tenant from the host name; without one there is no menu or zone to use.
        if (tenant.CompanyId is null)
        {
            throw new NotFoundException(ErrorCodes.ShopNotFound);
        }

        await validator.ValidateAndThrowAsync(request, ct).ConfigureAwait(false);

        var (address, zone, location) = await resolver
            .ResolveAsync(new AddressQuery(request.Street, request.PostalCode, request.City), ct)
            .ConfigureAwait(false);
        var pointDto = GeoPointDto.From(address.Point);
        if (zone is null)
        {
            return new DeliveryQuoteDto(false, pointDto, address.FormattedAddress, null, null, null, null, null, null, null);
        }

        return new DeliveryQuoteDto(
            true,
            pointDto,
            address.FormattedAddress,
            zone.LocationId,
            location!.Name,
            zone.Id,
            zone.Name,
            zone.DeliveryFee,
            zone.MinOrderValue,
            zone.EstimatedMinutes);
    }
}
