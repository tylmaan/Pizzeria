using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pizzeria.Application.Abstractions;
using Pizzeria.Application.Delivery;
using Pizzeria.Application.Errors;
using Pizzeria.Application.Locations;
using Pizzeria.Application.Menu;
using Pizzeria.Application.Payments;
using Pizzeria.Application.Tenancy;
using Pizzeria.Domain.Common;
using Pizzeria.Domain.Menu;
using Pizzeria.Domain.Orders;
using Pizzeria.Domain.Tenancy;
using Location = Pizzeria.Domain.Tenancy.Location;

namespace Pizzeria.Application.Orders;

/// <summary>Public shop data for the company resolved from the host name.</summary>
public sealed class ShopService(IAppDbContext db, ITenantContext tenant, MenuService menu, IPaymentProviderResolver paymentProviders)
{
    public async Task<ShopDto> GetAsync(CancellationToken ct)
    {
        if (tenant.CompanyId is null)
        {
            throw new NotFoundException(ErrorCodes.ShopNotFound);
        }

        var company = await db.Companies.AsNoTracking().SingleAsync(ct).ConfigureAwait(false);
        var locations = await db.Locations.AsNoTracking()
            .Where(l => l.IsActive)
            .OrderBy(l => l.Name)
            .ToListAsync(ct).ConfigureAwait(false);
        var full = await menu.GetAsync(ct).ConfigureAwait(false);

        // Customers only see what they can order: active products and modifiers, groups still in use.
        var products = full.Products.Where(p => p.IsActive).ToList();
        var usedGroupIds = products.SelectMany(p => p.ModifierGroupIds).ToHashSet();
        var groups = full.ModifierGroups
            .Where(g => usedGroupIds.Contains(g.Id))
            .Select(g => g with { Modifiers = [.. g.Modifiers.Where(m => m.IsActive)] })
            .ToList();
        var categories = full.Categories.Where(c => products.Exists(p => p.CategoryId == c.Id)).ToList();

        return new ShopDto(
            company.Name,
            locations.ConvertAll(l => new ShopLocationDto(l.Id, l.Name, AddressDto.From(l.Address))),
            full with { Categories = categories, Products = products, ModifierGroups = groups },
            paymentProviders.ForCompany(company.Id) is not null);
    }
}

/// <summary>
/// Places an order from the shop (anonymous, <see cref="OrderSource.Online"/>) or taken by staff on the phone.
/// Every price comes from the current menu via <see cref="ItemPricing"/>.
/// </summary>
public sealed class PlaceOrderService(
    IAppDbContext db,
    ITenantContext tenant,
    TimeProvider clock,
    DeliveryResolver deliveryResolver,
    IOrderNumberGenerator numbers,
    IOrderNotifier notifier,
    IPaymentProviderResolver paymentProviders,
    PaymentService payments,
    IValidator<PlaceOrderRequest> validator)
{
    public async Task<PlacedOrderDto> PlaceAsync(PlaceOrderRequest request, OrderSource source, CancellationToken ct)
    {
        if (tenant.CompanyId is null)
        {
            throw new NotFoundException(ErrorCodes.ShopNotFound);
        }

        await validator.ValidateAndThrowAsync(request, ct).ConfigureAwait(false);

        // Online payment: only for shop orders and only when the company has a payment provider set up.
        var paysOnline = request.PaymentMethod == PaymentMethod.Online;
        if (paysOnline && (source != OrderSource.Online || paymentProviders.ForCompany(tenant.CompanyId.Value) is null))
        {
            throw new UnprocessableException(OrderErrors.PaymentMethodNotAvailable);
        }

        var (location, delivery) = await ResolveFulfillmentAsync(request, ct).ConfigureAwait(false);
        var lines = await PriceItemsAsync(request.Items, ct).ConfigureAwait(false);

        var now = clock.GetUtcNow();
        var number = await numbers.NextAsync(location.Id, BusinessDay(location, now), ct).ConfigureAwait(false);
        var order = Order.Place(
            location,
            number,
            source,
            new OrderCustomer(request.Customer.Name, request.Customer.Phone),
            delivery,
            lines,
            request.PaymentMethod,
            request.Notes,
            tenant.UserId,
            now);

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await notifier.OrderChangedAsync(order, ct).ConfigureAwait(false);

        var redirect = paysOnline ? await payments.StartAsync(order, ct).ConfigureAwait(false) : null;
        return new PlacedOrderDto(order.Id, order.Number, order.TrackingToken, order.Total, redirect?.RedirectUrl);
    }

    /// <summary>"Day" of a location for numbering and reports is its local calendar day.</summary>
    private static DateOnly BusinessDay(Location location, DateTimeOffset now) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(location.TimeZoneId)).DateTime);

    private async Task<(Location Location, DeliveryTerms? Delivery)> ResolveFulfillmentAsync(PlaceOrderRequest request, CancellationToken ct)
    {
        if (request.Fulfillment == FulfillmentType.Pickup)
        {
            var location = await db.Locations
                .FirstOrDefaultAsync(l => l.Id == request.PickupLocationId && l.IsActive, ct).ConfigureAwait(false)
                ?? throw new NotFoundException(ErrorCodes.LocationNotFound);
            return (location, null);
        }

        var input = request.DeliveryAddress!;
        var resolved = await deliveryResolver
            .ResolveAsync(new AddressQuery(input.Street, input.PostalCode, input.City), ct)
            .ConfigureAwait(false);
        if (resolved.Zone is null)
        {
            throw new UnprocessableException(ErrorCodes.AddressNotDelivered);
        }

        var terms = new DeliveryTerms(
            new Address(input.Street.Trim(), input.PostalCode.Trim(), input.City.Trim()),
            resolved.Address.Point,
            input.Notes,
            resolved.Zone.DeliveryFee,
            resolved.Zone.MinOrderValue);
        return (resolved.Location!, terms);
    }

    private async Task<List<OrderLine>> PriceItemsAsync(IReadOnlyList<OrderItemInput> items, CancellationToken ct)
    {
        var company = await db.Companies.AsNoTracking().SingleAsync(ct).ConfigureAwait(false);

        var productIds = items.Select(i => i.ProductId)
            .Concat(items.Where(i => i.SecondHalfProductId is not null).Select(i => i.SecondHalfProductId!.Value))
            .Distinct()
            .ToList();
        var products = await db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct).ConfigureAwait(false);

        var groupIds = products.Values.SelectMany(p => p.ModifierGroups.Select(g => g.ModifierGroupId)).Distinct().ToList();
        var groups = await db.ModifierGroups.AsNoTracking().Where(g => groupIds.Contains(g.Id)).ToListAsync(ct).ConfigureAwait(false);

        var categoryIds = products.Values.Select(p => p.CategoryId).Distinct().ToList();
        var sizeNames = (await db.Categories.AsNoTracking().Where(c => categoryIds.Contains(c.Id)).ToListAsync(ct).ConfigureAwait(false))
            .SelectMany(c => c.Sizes)
            .ToDictionary(s => s.Id, s => s.Name);

        return [.. items.Select(item =>
        {
            // A product of another company is invisible through the tenant filter: "unavailable" to this shop.
            var product = products.GetValueOrDefault(item.ProductId)
                ?? throw new DomainException(MenuErrors.ProductUnavailable);
            var secondHalf = item.SecondHalfProductId is { } secondId
                ? products.GetValueOrDefault(secondId) ?? throw new DomainException(MenuErrors.ProductUnavailable)
                : null;

            var price = ItemPricing.Price(product, secondHalf, item.SizeId, groups, [.. item.ModifierIds], company.HalfAndHalfPricing);
            return new OrderLine(
                product.Id,
                product.Name,
                secondHalf?.Id,
                secondHalf?.Name,
                item.SizeId,
                sizeNames.GetValueOrDefault(item.SizeId) ?? throw new DomainException(MenuErrors.SizeUnavailable),
                item.Quantity,
                price,
                item.Notes);
        })];
    }
}

/// <summary>Customer order tracking by the secret token from the confirmation link (also for guests).</summary>
public sealed class OrderTrackingService(IAppDbContext db)
{
    public async Task<OrderTrackingDto> GetAsync(string token, CancellationToken ct)
    {
        // The tenant filter (company from the shop host) still applies: a token of another company is "not found".
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.TrackingToken == token, ct).ConfigureAwait(false)
            ?? throw new NotFoundException(ErrorCodes.OrderNotFound);
        var locationName = await db.Locations.Where(l => l.Id == order.LocationId).Select(l => l.Name).SingleAsync(ct).ConfigureAwait(false);

        return new OrderTrackingDto(
            order.Number,
            order.Status,
            order.Fulfillment,
            locationName,
            order.PlacedAt,
            OrderItemDto.FromAll(order),
            order.ItemsTotal,
            order.DeliveryFee,
            order.Total,
            order.PaymentMethod,
            order.PaymentStatus,
            order.DeliveryAddress is { } a ? AddressDto.From(a) : null,
            order.CancellationReason,
            [.. order.StatusHistory
                .Where(h => h.FromStatus != h.ToStatus)
                .OrderBy(h => h.ChangedAt)
                .Select(h => new OrderStatusChangeDto(h.ToStatus, h.ChangedAt))]);
    }
}
