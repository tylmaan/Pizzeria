namespace Pizzeria.Domain.Tenancy;

public sealed record Address(string Street, string PostalCode, string City)
{
    public const int StreetMaxLength = 200;
    public const int PostalCodeMaxLength = 10;
    public const int CityMaxLength = 100;
}
