using FluentValidation;
using Pizzeria.Domain.Identity;

namespace Pizzeria.Application.Users;

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public const int DisplayNameMaxLength = 100;

    public CreateUserRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(r => r.Password).NotEmpty();
        RuleFor(r => r.DisplayName).NotEmpty().MaximumLength(DisplayNameMaxLength);
        RuleFor(r => r.Role).NotEmpty().Must(role => Roles.All.Contains(role)).WithErrorCode("validation.role");
    }
}
