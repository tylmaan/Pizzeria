using Pizzeria.Domain.Common;
using Pizzeria.Domain.Delivery;
using Pizzeria.Domain.Tenancy;

namespace Pizzeria.Domain.Tests.Delivery;

public sealed class DeliveryZoneTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    private readonly Location _location = Location.Create(
        Guid.NewGuid(), "Centrum", new Address("ul. Lipowa 1", "15-001", "Białystok"), null, Now);

    [Fact]
    public void Covers_PointInsideSquare()
    {
        var zone = Zone(Square(53.13, 23.16, 0.01));

        Assert.True(zone.Covers(new GeoPoint(53.13, 23.16)));
        Assert.False(zone.Covers(new GeoPoint(53.20, 23.16)));
    }

    [Fact]
    public void Covers_PointOnBorderCounts()
    {
        var zone = Zone(Square(53.13, 23.16, 0.01));

        Assert.True(zone.Covers(new GeoPoint(53.14, 23.16)));
    }

    [Fact]
    public void Boundary_RoundTripsWithoutClosingPoint()
    {
        var square = Square(53.13, 23.16, 0.01);

        var zone = Zone([.. square, square[0]]);

        Assert.Equal(4, zone.Boundary.Count);
        Assert.All(square, p => Assert.Contains(p, zone.Boundary));
    }

    [Theory]
    [MemberData(nameof(InvalidBoundaries))]
    public void Create_InvalidBoundary_Throws(GeoPoint[] boundary)
    {
        var ex = Assert.Throws<DomainException>(() => Zone(boundary));

        Assert.Equal(DeliveryErrors.InvalidBoundary, ex.Code);
    }

    public static TheoryData<GeoPoint[]> InvalidBoundaries() => new()
    {
        new GeoPoint[] { new(53.1, 23.1), new(53.2, 23.2) },
        // Bow tie: edges cross each other.
        new GeoPoint[] { new(53.10, 23.10), new(53.12, 23.12), new(53.10, 23.12), new(53.12, 23.10) },
        // All points on one line: no area.
        new GeoPoint[] { new(53.10, 23.10), new(53.11, 23.11), new(53.12, 23.12) },
        new GeoPoint[] { new(95, 23.10), new(53.11, 23.11), new(53.12, 23.10) },
        new GeoPoint[] { new(53.10, 23.10), new(53.10, 23.10), new(53.12, 23.12), new(53.12, 23.10) },
    };

    [Theory]
    [InlineData(0)]
    [InlineData(601)]
    public void Create_InvalidEstimate_Throws(int minutes)
    {
        var ex = Assert.Throws<DomainException>(() =>
            DeliveryZone.Create(_location, "Centrum", Square(53.13, 23.16, 0.01), 5m, 30m, minutes, Now));

        Assert.Equal(DeliveryErrors.InvalidEstimate, ex.Code);
    }

    [Fact]
    public void PickFor_OverlappingZones_PrefersSmallestActiveArea()
    {
        var city = Zone(Square(53.13, 23.16, 0.05), "Miasto");
        var center = Zone(Square(53.13, 23.16, 0.01), "Centrum");
        var inactiveTiny = Zone(Square(53.13, 23.16, 0.001), "Wyłączona");
        inactiveTiny.Update("Wyłączona", Square(53.13, 23.16, 0.001), 0m, 0m, 20, isActive: false);

        Assert.Same(center, DeliveryZone.PickFor([city, inactiveTiny, center], new GeoPoint(53.13, 23.16)));
        Assert.Same(city, DeliveryZone.PickFor([city, center], new GeoPoint(53.17, 23.16)));
        Assert.Null(DeliveryZone.PickFor([city, center], new GeoPoint(52.0, 21.0)));
    }

    private DeliveryZone Zone(IReadOnlyList<GeoPoint> boundary, string name = "Strefa") =>
        DeliveryZone.Create(_location, name, boundary, 5m, 30m, 45, Now);

    private static GeoPoint[] Square(double lat, double lon, double half) =>
    [
        new(lat - half, lon - half),
        new(lat - half, lon + half),
        new(lat + half, lon + half),
        new(lat + half, lon - half),
    ];
}
