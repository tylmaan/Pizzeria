using FluentValidation;
using Pizzeria.Application.Users;
using Pizzeria.Domain.Tenancy;

namespace Pizzeria.Application.Platform;

public sealed class CreateCompanyRequestValidator : AbstractValidator<CreateCompanyRequest>
{
    public CreateCompanyRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(Company.NameMaxLength);
        RuleFor(r => r.Slug)
            .NotEmpty()
            .Length(2, Company.SlugMaxLength)
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
            .WithErrorCode("validation.slug");
        RuleFor(r => r.AdminEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(r => r.AdminPassword).NotEmpty();
        RuleFor(r => r.AdminDisplayName).NotEmpty().MaximumLength(CreateUserRequestValidator.DisplayNameMaxLength);
    }
}
