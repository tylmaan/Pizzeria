using NetTopologySuite;
using NetTopologySuite.Geometries;
using Pizzeria.Domain.Common;
using Pizzeria.Domain.Menu;
using Location = Pizzeria.Domain.Tenancy.Location;

namespace Pizzeria.Domain.Delivery;

public static class DeliveryErrors
{
    public const string InvalidBoundary = "delivery.invalid_boundary";
    public const string InvalidEstimate = "delivery.invalid_estimate";
    public const string InvalidName = "delivery.invalid_name";
}

/// <summary>WGS 84 point (GPS coordinates).</summary>
public readonly record struct GeoPoint(double Latitude, double Longitude)
{
    public bool IsValid => Latitude is >= -90 and <= 90 && Longitude is >= -180 and <= 180
                           && double.IsFinite(Latitude) && double.IsFinite(Longitude);

    /// <summary>NTS/PostGIS order is X = longitude, Y = latitude.</summary>
    public Point ToPoint() => Geo.Factory.CreatePoint(new Coordinate(Longitude, Latitude));

    public static GeoPoint From(Coordinate coordinate)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        return new GeoPoint(coordinate.Y, coordinate.X);
    }
}

public static class Geo
{
    /// <summary>SRID 4326 = WGS 84, the coordinate system of GPS and web maps.</summary>
    public const int Srid = 4326;

    public static GeometryFactory Factory { get; } = NtsGeometryServices.Instance.CreateGeometryFactory(Srid);
}

/// <summary>
/// Area a location delivers to, with its delivery fee, minimum order value and estimated delivery time.
/// </summary>
public sealed class DeliveryZone : ILocationEntity
{
    public const int NameMaxLength = 100;
    public const int MaxBoundaryPoints = 500;
    private const double MinAreaSquareDegrees = 1e-9;

    private DeliveryZone()
    {
    }

    public Guid Id { get; private set; }

    public Guid CompanyId { get; private set; }

    public Guid LocationId { get; private set; }

    public string Name { get; private set; } = null!;

    public Polygon Area { get; private set; } = null!;

    public decimal DeliveryFee { get; private set; }

    public decimal MinOrderValue { get; private set; }

    public int EstimatedMinutes { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static DeliveryZone Create(
        Location location,
        string name,
        IReadOnlyList<GeoPoint> boundary,
        decimal deliveryFee,
        decimal minOrderValue,
        int estimatedMinutes,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(location);

        var zone = new DeliveryZone
        {
            Id = Guid.CreateVersion7(now),
            CompanyId = location.CompanyId,
            LocationId = location.Id,
            CreatedAt = now,
        };
        zone.Update(name, boundary, deliveryFee, minOrderValue, estimatedMinutes, isActive: true);
        return zone;
    }

    public void Update(
        string name,
        IReadOnlyList<GeoPoint> boundary,
        decimal deliveryFee,
        decimal minOrderValue,
        int estimatedMinutes,
        bool isActive)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Length > NameMaxLength)
        {
            throw new DomainException(DeliveryErrors.InvalidName);
        }

        if (estimatedMinutes is < 1 or > 600)
        {
            throw new DomainException(DeliveryErrors.InvalidEstimate);
        }

        Name = trimmed;
        Area = ToPolygon(boundary);
        DeliveryFee = Money.RequirePrice(deliveryFee);
        MinOrderValue = Money.RequirePrice(minOrderValue);
        EstimatedMinutes = estimatedMinutes;
        IsActive = isActive;
    }

    public bool Covers(GeoPoint point) => Area.Covers(point.ToPoint());

    /// <summary>The boundary as drawn (without the closing point repeated).</summary>
    public IReadOnlyList<GeoPoint> Boundary =>
        [.. Area.ExteriorRing.Coordinates.Take(Area.ExteriorRing.NumPoints - 1).Select(GeoPoint.From)];

    /// <summary>
    /// When zones overlap (also zones of different locations), the most specific one wins: the smallest area.
    /// </summary>
    public static DeliveryZone? PickFor(IEnumerable<DeliveryZone> candidates, GeoPoint point) =>
        candidates
            .Where(z => z.IsActive && z.Covers(point))
            .OrderBy(z => z.Area.Area)
            .ThenBy(z => z.Id)
            .FirstOrDefault();

    private static Polygon ToPolygon(IReadOnlyList<GeoPoint> boundary)
    {
        ArgumentNullException.ThrowIfNull(boundary);

        var points = boundary.ToList();
        if (points.Count > 1 && points[0] == points[^1])
        {
            points.RemoveAt(points.Count - 1);
        }

        if (points.Count < 3 || points.Count > MaxBoundaryPoints || points.Any(p => !p.IsValid)
            || points.Distinct().Count() != points.Count)
        {
            throw new DomainException(DeliveryErrors.InvalidBoundary);
        }

        var ring = points.Select(p => new Coordinate(p.Longitude, p.Latitude)).Append(new Coordinate(points[0].Longitude, points[0].Latitude));
        var polygon = Geo.Factory.CreatePolygon([.. ring]);

        // Self-intersecting ("bow tie") or degenerate shapes cannot answer "is this address inside".
        // Area is in square degrees: 1e-9 is a few square metres, so collinear points (rounding noise) fail.
        if (!polygon.IsValid || polygon.Area < MinAreaSquareDegrees)
        {
            throw new DomainException(DeliveryErrors.InvalidBoundary);
        }

        return (Polygon)polygon.Normalized();
    }
}
