using FluentValidation;
using Pizzeria.Domain.Orders;
using Pizzeria.Domain.Tenancy;

namespace Pizzeria.Application.Orders;

public sealed class PlaceOrderRequestValidator : AbstractValidator<PlaceOrderRequest>
{
    public const int MaxItems = 50;

    public PlaceOrderRequestValidator()
    {
        RuleFor(r => r.Fulfillment).IsInEnum();
        RuleFor(r => r.PaymentMethod).IsInEnum();
        RuleFor(r => r.Notes).MaximumLength(Order.NotesMaxLength);

        RuleFor(r => r.Customer).NotNull();
        RuleFor(r => r.Customer.Name).NotEmpty().MaximumLength(Order.CustomerNameMaxLength).When(r => r.Customer is not null);
        RuleFor(r => r.Customer.Phone)
            .NotEmpty()
            .Matches(@"^\+?[0-9][0-9 \-]{7,18}$")
            .WithErrorCode("validation.phone")
            .When(r => r.Customer is not null);

        RuleFor(r => r.PickupLocationId)
            .NotEmpty()
            .When(r => r.Fulfillment == FulfillmentType.Pickup)
            .WithErrorCode("validation.location_required");

        RuleFor(r => r.DeliveryAddress)
            .NotNull()
            .When(r => r.Fulfillment == FulfillmentType.Delivery)
            .WithErrorCode("validation.address_required");
        RuleFor(r => r.DeliveryAddress!).ChildRules(address =>
        {
            address.RuleFor(a => a.Street).NotEmpty().MaximumLength(Address.StreetMaxLength);
            address.RuleFor(a => a.City).NotEmpty().MaximumLength(Address.CityMaxLength);
            address.RuleFor(a => a.PostalCode).NotEmpty().Matches(@"^\d{2}-\d{3}$").WithErrorCode("validation.postal_code");
            address.RuleFor(a => a.Notes).MaximumLength(Order.NotesMaxLength);
        }).When(r => r.Fulfillment == FulfillmentType.Delivery && r.DeliveryAddress is not null);

        RuleFor(r => r.Items).NotEmpty().Must(items => items.Count <= MaxItems).WithErrorCode("validation.too_many");
        RuleForEach(r => r.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty();
            item.RuleFor(i => i.SizeId).NotEmpty();
            item.RuleFor(i => i.Quantity).InclusiveBetween(1, OrderItem.MaxQuantity);
            item.RuleFor(i => i.Notes).MaximumLength(OrderItem.NotesMaxLength);
        });
    }
}
