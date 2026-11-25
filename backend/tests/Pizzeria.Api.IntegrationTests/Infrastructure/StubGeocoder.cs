using System.Collections.Concurrent;
using Pizzeria.Application.Delivery;
using Pizzeria.Domain.Delivery;

namespace Pizzeria.Api.IntegrationTests.Infrastructure;

/// <summary>Geocoder with addresses registered by tests (keyed by street); unknown streets are "not found".</summary>
public sealed class StubGeocoder : IGeocodingService
{
    private readonly ConcurrentDictionary<string, GeoPoint> _streets = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers a unique street at the given point and returns the street name to use in requests.</summary>
    public string Place(double latitude, double longitude)
    {
        var street = $"ul. Testowa {Guid.NewGuid():N}";
        _streets[street] = new GeoPoint(latitude, longitude);
        return street;
    }

    public Task<GeocodedAddress?> GeocodeAsync(AddressQuery address, CancellationToken ct) =>
        Task.FromResult(_streets.TryGetValue(address.Street, out var point)
            ? new GeocodedAddress(point, $"{address.Street}, {address.PostalCode} {address.City}")
            : null);
}
