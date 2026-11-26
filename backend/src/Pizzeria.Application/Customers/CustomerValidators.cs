using FluentValidation;
using Pizzeria.Domain.Customers;
using Pizzeria.Domain.Orders;
using Pizzeria.Domain.Tenancy;

namespace Pizzeria.Application.Customers;

internal static class CustomerRules
{
    // Same format as the phone on an order, so a saved profile always passes checkout.
    public const string PhonePattern = @"^\+?[0-9][0-9 \-]{7,18}$";

    // User names are "{company}+{email}" and Identity allows 256 characters.
    public const int EmailMaxLength = 200;
}

public sealed class RegisterCustomerRequestValidator : AbstractValidator<RegisterCustomerRequest>
{
    public RegisterCustomerRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(CustomerRules.EmailMaxLength);
        RuleFor(r => r.Password).NotEmpty();
        RuleFor(r => r.Name).NotEmpty().MaximumLength(Order.CustomerNameMaxLength);
        RuleFor(r => r.Phone!).Matches(CustomerRules.PhonePattern).WithErrorCode("validation.phone").When(r => !string.IsNullOrWhiteSpace(r.Phone));
    }
}

public sealed class UpdateCustomerProfileRequestValidator : AbstractValidator<UpdateCustomerProfileRequest>
{
    public UpdateCustomerProfileRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(Order.CustomerNameMaxLength);
        RuleFor(r => r.Phone!).Matches(CustomerRules.PhonePattern).WithErrorCode("validation.phone").When(r => !string.IsNullOrWhiteSpace(r.Phone));
    }
}

public sealed class SaveAddressRequestValidator : AbstractValidator<SaveAddressRequest>
{
    public SaveAddressRequestValidator()
    {
        RuleFor(r => r.Label).MaximumLength(SavedAddress.LabelMaxLength);
        RuleFor(r => r.Street).NotEmpty().MaximumLength(Address.StreetMaxLength);
        RuleFor(r => r.City).NotEmpty().MaximumLength(Address.CityMaxLength);
        RuleFor(r => r.PostalCode).NotEmpty().Matches(@"^\d{2}-\d{3}$").WithErrorCode("validation.postal_code");
        RuleFor(r => r.Notes).MaximumLength(SavedAddress.NotesMaxLength);
    }
}
