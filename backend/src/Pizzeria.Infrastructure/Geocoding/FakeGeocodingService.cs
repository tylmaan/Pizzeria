using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Pizzeria.Application.Delivery;
using Pizzeria.Domain.Delivery;

namespace Pizzeria.Infrastructure.Geocoding;

public sealed class GeocodingOptions
{
    public const string SectionName = "Geocoding";

    /// <summary>Only <c>Fake</c> for now; the real provider is an open decision.</summary>
    public string Provider { get; set; } = "Fake";

    public FakeGeocodingOptions Fake { get; set; } = new();
}

public sealed class FakeGeocodingOptions
{
    /// <summary>Addresses land around this point (default: Białystok city centre).</summary>
    public double CenterLatitude { get; set; } = 53.1325;

    public double CenterLongitude { get; set; } = 23.1688;

    public double RadiusKm { get; set; } = 4;
}

/// <summary>
/// Development stand-in for a real geocoder: every address maps to a stable point within
/// <see cref="FakeGeocodingOptions.RadiusKm"/> of the centre (same address → same point), so delivery
/// zones drawn around the centre catch some addresses and not others.
/// </summary>
public sealed class FakeGeocodingService(IOptions<GeocodingOptions> options) : IGeocodingService
{
    private const double KmPerDegreeLatitude = 111.32;

    public Task<GeocodedAddress?> GeocodeAsync(AddressQuery address, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(address);

        var normalized = string.Join('|', address.Street, address.PostalCode, address.City)
            .Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(address.Street) || string.IsNullOrWhiteSpace(address.City))
        {
            return Task.FromResult<GeocodedAddress?>(null);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        var angle = BitConverter.ToUInt32(hash, 0) / (double)uint.MaxValue * 2 * Math.PI;
        // sqrt spreads points evenly over the disc instead of crowding the centre.
        var distanceKm = Math.Sqrt(BitConverter.ToUInt32(hash, 4) / (double)uint.MaxValue) * options.Value.Fake.RadiusKm;

        var center = options.Value.Fake;
        var latitude = center.CenterLatitude + distanceKm * Math.Cos(angle) / KmPerDegreeLatitude;
        var longitude = center.CenterLongitude
                        + distanceKm * Math.Sin(angle) / (KmPerDegreeLatitude * Math.Cos(center.CenterLatitude * Math.PI / 180));

        var formatted = string.Create(CultureInfo.InvariantCulture, $"{address.Street.Trim()}, {address.PostalCode.Trim()} {address.City.Trim()}");
        return Task.FromResult<GeocodedAddress?>(new GeocodedAddress(new GeoPoint(latitude, longitude), formatted));
    }
}
