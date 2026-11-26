using NetTopologySuite.Geometries;
using Pizzeria.Domain.Common;
using Pizzeria.Domain.Delivery;

namespace Pizzeria.Domain.Drivers;

/// <summary>
/// One GPS fix of a driver. Recorded only during an open shift (we never track drivers off duty);
/// the trail is deleted after the retention period (GDPR), see <c>DriverPositionRetention</c>.
/// </summary>
public sealed class DriverPosition : ILocationEntity
{
    private DriverPosition()
    {
    }

    public Guid Id { get; private set; }

    public Guid CompanyId { get; private set; }

    public Guid LocationId { get; private set; }

    public Guid DriverId { get; private set; }

    public Guid ShiftId { get; private set; }

    public Point Point { get; private set; } = null!;

    /// <summary>Accuracy reported by the phone, in metres.</summary>
    public double? AccuracyMeters { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    public GeoPoint Position => GeoPoint.From(Point.Coordinate);

    public static DriverPosition Record(DriverShift shift, GeoPoint point, double? accuracyMeters, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(shift);

        if (shift.Status != DriverShiftStatus.Open)
        {
            throw new DomainException(DriverShiftErrors.NotOpen);
        }

        if (!point.IsValid || accuracyMeters is < 0 or > 100_000)
        {
            throw new DomainException(DriverShiftErrors.InvalidPosition);
        }

        return new DriverPosition
        {
            Id = Guid.CreateVersion7(now),
            CompanyId = shift.CompanyId,
            LocationId = shift.LocationId,
            DriverId = shift.DriverId,
            ShiftId = shift.Id,
            Point = point.ToPoint(),
            AccuracyMeters = accuracyMeters,
            RecordedAt = now,
        };
    }
}
