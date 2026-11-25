using Pizzeria.Domain.Delivery;

namespace Pizzeria.Application.Delivery;

public sealed record GeoPointDto(double Latitude, double Longitude)
{
    public static GeoPointDto From(GeoPoint p) => new(p.Latitude, p.Longitude);

    public GeoPoint ToDomain() => new(Latitude, Longitude);
}

public sealed record DeliveryZoneDto(
    Guid Id,
    Guid LocationId,
    string Name,
    IReadOnlyList<GeoPointDto> Boundary,
    decimal DeliveryFee,
    decimal MinOrderValue,
    int EstimatedMinutes,
    bool IsActive)
{
    public static DeliveryZoneDto From(DeliveryZone z) => new(
        z.Id,
        z.LocationId,
        z.Name,
        [.. z.Boundary.Select(GeoPointDto.From)],
        z.DeliveryFee,
        z.MinOrderValue,
        z.EstimatedMinutes,
        z.IsActive);
}

public sealed record SaveDeliveryZoneRequest(
    string Name,
    IReadOnlyList<GeoPointDto> Boundary,
    decimal DeliveryFee,
    decimal MinOrderValue,
    int EstimatedMinutes,
    bool IsActive);

public sealed record DeliveryCheckRequest(string Street, string PostalCode, string City);

/// <summary>Whether and on what terms the company delivers to an address.</summary>
public sealed record DeliveryQuoteDto(
    bool Delivers,
    GeoPointDto Point,
    string FormattedAddress,
    Guid? LocationId,
    string? LocationName,
    Guid? ZoneId,
    decimal? DeliveryFee,
    decimal? MinOrderValue,
    int? EstimatedMinutes);
