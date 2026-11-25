using FluentValidation;
using Pizzeria.Domain.Tenancy;

namespace Pizzeria.Application.Locations;

public sealed class AddressDtoValidator : AbstractValidator<AddressDto>
{
    public AddressDtoValidator()
    {
        RuleFor(a => a.Street).NotEmpty().MaximumLength(Address.StreetMaxLength);
        RuleFor(a => a.City).NotEmpty().MaximumLength(Address.CityMaxLength);
        RuleFor(a => a.PostalCode)
            .NotEmpty()
            .Matches(@"^\d{2}-\d{3}$")
            .WithErrorCode("validation.postal_code");
    }
}

public sealed class CreateLocationRequestValidator : AbstractValidator<CreateLocationRequest>
{
    public CreateLocationRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(Location.NameMaxLength);
        RuleFor(r => r.Address).NotNull().SetValidator(new AddressDtoValidator());
        RuleFor(r => r.TimeZoneId!)
            .Must(IsKnownTimeZone)
            .When(r => r.TimeZoneId is not null)
            .WithErrorCode("validation.time_zone");
    }

    internal static bool IsKnownTimeZone(string timeZoneId) => TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _);
}

public sealed class UpdateLocationRequestValidator : AbstractValidator<UpdateLocationRequest>
{
    public UpdateLocationRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(Location.NameMaxLength);
        RuleFor(r => r.Address).NotNull().SetValidator(new AddressDtoValidator());
        RuleFor(r => r.TimeZoneId)
            .NotEmpty()
            .Must(CreateLocationRequestValidator.IsKnownTimeZone)
            .WithErrorCode("validation.time_zone");
    }
}
