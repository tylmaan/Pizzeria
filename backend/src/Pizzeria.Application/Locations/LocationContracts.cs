using Pizzeria.Application.Delivery;
using Pizzeria.Domain.Tenancy;

namespace Pizzeria.Application.Locations;

public sealed record AddressDto(string Street, string PostalCode, string City)
{
    public static AddressDto From(Address address) => new(address.Street, address.PostalCode, address.City);

    public Address ToDomain() => new(Street, PostalCode, City);
}

public sealed record LocationDto(Guid Id, string Name, AddressDto Address, string TimeZoneId, bool IsActive, GeoPointDto? Position)
{
    public static LocationDto From(Location location) => new(
        location.Id,
        location.Name,
        AddressDto.From(location.Address),
        location.TimeZoneId,
        location.IsActive,
        location.Position is { } p ? new GeoPointDto(p.Y, p.X) : null);
}

public sealed record CreateLocationRequest(string Name, AddressDto Address, string? TimeZoneId);

public sealed record UpdateLocationRequest(string Name, AddressDto Address, string TimeZoneId, bool IsActive);
