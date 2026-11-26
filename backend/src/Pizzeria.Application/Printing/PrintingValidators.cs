using FluentValidation;
using Pizzeria.Domain.Printing;

namespace Pizzeria.Application.Printing;

public sealed class RegisterAgentRequestValidator : AbstractValidator<RegisterAgentRequest>
{
    public RegisterAgentRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(PrintAgent.NameMaxLength);
    }
}

public sealed class PrintSettingsDtoValidator : AbstractValidator<PrintSettingsDto>
{
    public PrintSettingsDtoValidator()
    {
        RuleFor(r => r.FiscalReceiptTrigger).IsInEnum();
    }
}
