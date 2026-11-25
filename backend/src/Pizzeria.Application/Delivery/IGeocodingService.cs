using Pizzeria.Domain.Delivery;

namespace Pizzeria.Application.Delivery;

public sealed record AddressQuery(string Street, string PostalCode, string City);

public sealed record GeocodedAddress(GeoPoint Point, string FormattedAddress);

/// <summary>
/// Address → coordinates. The provider is an open decision (Google / Mapbox / Nominatim, see CLAUDE.md);
/// until then a deterministic fake is used.
/// </summary>
public interface IGeocodingService
{
    /// <returns>The location of the address, or <c>null</c> when it cannot be found.</returns>
    Task<GeocodedAddress?> GeocodeAsync(AddressQuery address, CancellationToken ct);
}
