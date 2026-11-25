using Pizzeria.Domain.Common;

namespace Pizzeria.Domain.Tenancy;

/// <summary>A single restaurant of a company. Has its own address, time zone, staff and delivery zones.</summary>
public sealed class Location : ITenantEntity
{
    public const int NameMaxLength = 200;
    public const string DefaultTimeZoneId = "Europe/Warsaw";

    private Location()
    {
    }

    public Guid Id { get; private set; }

    public Guid CompanyId { get; private set; }

    public string Name { get; private set; } = null!;

    public Address Address { get; private set; } = null!;

    /// <summary>IANA time zone id. Used to compute the local "business day" for reports.</summary>
    public string TimeZoneId { get; private set; } = DefaultTimeZoneId;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Location Create(Guid companyId, string name, Address address, string? timeZoneId, DateTimeOffset now)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("Company id is required.", nameof(companyId));
        }

        var location = new Location
        {
            Id = Guid.CreateVersion7(now),
            CompanyId = companyId,
            IsActive = true,
            CreatedAt = now,
        };
        location.Update(name, address, timeZoneId ?? DefaultTimeZoneId);
        return location;
    }

    public void Update(string name, Address address, string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(address);

        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length == 0 || trimmedName.Length > NameMaxLength)
        {
            throw new DomainException(TenancyErrors.InvalidName);
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _))
        {
            throw new DomainException(TenancyErrors.InvalidTimeZone);
        }

        Name = trimmedName;
        Address = address with
        {
            Street = address.Street.Trim(),
            PostalCode = address.PostalCode.Trim(),
            City = address.City.Trim(),
        };
        TimeZoneId = timeZoneId;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
