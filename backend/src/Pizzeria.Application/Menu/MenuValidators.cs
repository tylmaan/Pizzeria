using FluentValidation;
using Pizzeria.Domain.Menu;

namespace Pizzeria.Application.Menu;

public sealed class PriceDtoValidator : AbstractValidator<PriceDto>
{
    public PriceDtoValidator()
    {
        RuleFor(p => p.Price)
            .InclusiveBetween(0m, Money.MaxPrice)
            .PrecisionScale(10, 2, ignoreTrailingZeros: true)
            .WithErrorCode("validation.price");
    }
}

public sealed class SaveCategoryRequestValidator : AbstractValidator<SaveCategoryRequest>
{
    public SaveCategoryRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(Category.NameMaxLength);
        RuleFor(r => r.Sizes).NotEmpty().Must(s => s.Count <= Category.MaxSizes).WithErrorCode("validation.too_many");
        RuleForEach(r => r.Sizes).ChildRules(size =>
            size.RuleFor(s => s.Name).NotEmpty().MaximumLength(Category.SizeNameMaxLength));
    }
}

public sealed class SaveProductRequestValidator : AbstractValidator<SaveProductRequest>
{
    public SaveProductRequestValidator()
    {
        RuleFor(r => r.CategoryId).NotEmpty();
        RuleFor(r => r.Name).NotEmpty().MaximumLength(Product.NameMaxLength);
        RuleFor(r => r.Description).MaximumLength(Product.DescriptionMaxLength);
        RuleFor(r => r.Prices).NotEmpty().WithErrorCode("validation.prices_required");
        RuleForEach(r => r.Prices).SetValidator(new PriceDtoValidator());
    }
}

public sealed class SaveModifierGroupRequestValidator : AbstractValidator<SaveModifierGroupRequest>
{
    public SaveModifierGroupRequestValidator()
    {
        RuleFor(r => r.CategoryId).NotEmpty();
        RuleFor(r => r.Name).NotEmpty().MaximumLength(ModifierGroup.NameMaxLength);
        RuleFor(r => r.MinSelect).GreaterThanOrEqualTo(0);
        RuleFor(r => r.MaxSelect).GreaterThanOrEqualTo(1);
        RuleFor(r => r.MaxSelect)
            .GreaterThanOrEqualTo(r => r.MinSelect)
            .WithErrorCode("validation.max_below_min");
        RuleFor(r => r.MinSelect)
            .LessThanOrEqualTo(r => r.Modifiers.Count)
            .WithErrorCode("validation.min_above_modifier_count");
        RuleFor(r => r.Modifiers).NotEmpty().Must(m => m.Count <= ModifierGroup.MaxModifiers).WithErrorCode("validation.too_many");
        RuleForEach(r => r.Modifiers).ChildRules(modifier =>
        {
            modifier.RuleFor(m => m.Name).NotEmpty().MaximumLength(ModifierGroup.NameMaxLength);
            modifier.RuleFor(m => m.Prices).NotEmpty().WithErrorCode("validation.prices_required");
            modifier.RuleForEach(m => m.Prices).SetValidator(new PriceDtoValidator());
        });
    }
}
