using FluentValidation;
using Pizzeria.Domain.Delivery;
using Pizzeria.Domain.Menu;
using Pizzeria.Domain.Tenancy;

namespace Pizzeria.Application.Delivery;

public sealed class SaveDeliveryZoneRequestValidator : AbstractValidator<SaveDeliveryZoneRequest>
{
    public SaveDeliveryZoneRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(DeliveryZone.NameMaxLength);
        RuleFor(r => r.Boundary)
            .Must(b => b.Count is >= 3 and <= DeliveryZone.MaxBoundaryPoints)
            .WithErrorCode("validation.boundary");
        RuleForEach(r => r.Boundary)
            .Must(p => p.ToDomain().IsValid)
            .WithErrorCode("validation.coordinates");
        RuleFor(r => r.DeliveryFee)
            .InclusiveBetween(0m, Money.MaxPrice)
            .PrecisionScale(10, 2, ignoreTrailingZeros: true)
            .WithErrorCode("validation.price");
        RuleFor(r => r.MinOrderValue)
            .InclusiveBetween(0m, Money.MaxPrice)
            .PrecisionScale(10, 2, ignoreTrailingZeros: true)
            .WithErrorCode("validation.price");
        RuleFor(r => r.EstimatedMinutes).InclusiveBetween(1, 600);
    }
}

public sealed class DeliveryCheckRequestValidator : AbstractValidator<DeliveryCheckRequest>
{
    public DeliveryCheckRequestValidator()
    {
        RuleFor(r => r.Street).NotEmpty().MaximumLength(Address.StreetMaxLength);
        RuleFor(r => r.City).NotEmpty().MaximumLength(Address.CityMaxLength);
        RuleFor(r => r.PostalCode).NotEmpty().Matches(@"^\d{2}-\d{3}$").WithErrorCode("validation.postal_code");
    }
}
