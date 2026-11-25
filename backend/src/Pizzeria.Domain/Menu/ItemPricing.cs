using Pizzeria.Domain.Common;

namespace Pizzeria.Domain.Menu;

/// <summary>How a half-and-half pizza is priced. Company setting.</summary>
public enum HalfAndHalfPricing
{
    /// <summary>Price of the more expensive half (default).</summary>
    MoreExpensiveHalf,

    /// <summary>Average of both halves, rounded to grosze.</summary>
    AverageOfHalves,
}

public sealed record PricedModifier(Guid ModifierId, Guid ModifierGroupId, string Name, decimal Price);

public sealed record PricedItem(decimal BasePrice, IReadOnlyList<PricedModifier> Modifiers)
{
    /// <summary>Price of one piece: base price plus all chosen modifiers.</summary>
    public decimal UnitPrice => BasePrice + Modifiers.Sum(m => m.Price);
}

/// <summary>
/// Prices one order item from the current menu. The only place that turns a customer's selection into money;
/// prices sent by clients are never trusted.
/// </summary>
public static class ItemPricing
{
    /// <param name="product">The product, or the first half of a half-and-half pizza.</param>
    /// <param name="secondHalf">The second half, or <c>null</c> for a regular item.</param>
    /// <param name="sizeId">Chosen size (both halves are always the same size).</param>
    /// <param name="modifierGroups">Modifier groups attached to the product(s); others are ignored.</param>
    /// <param name="modifierIds">Chosen modifiers. For half-and-half they apply to the whole item.</param>
    /// <param name="halfAndHalfPricing">Company rule for half-and-half pricing.</param>
    public static PricedItem Price(
        Product product,
        Product? secondHalf,
        Guid sizeId,
        IReadOnlyCollection<ModifierGroup> modifierGroups,
        IReadOnlyCollection<Guid> modifierIds,
        HalfAndHalfPricing halfAndHalfPricing)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(modifierGroups);
        ArgumentNullException.ThrowIfNull(modifierIds);

        var basePrice = PriceOf(product, sizeId);
        var allowedGroupIds = product.ModifierGroups.Select(g => g.ModifierGroupId).ToHashSet();

        if (secondHalf is not null)
        {
            if (!product.AllowsHalves || !secondHalf.AllowsHalves || product.CategoryId != secondHalf.CategoryId)
            {
                throw new DomainException(MenuErrors.HalvesNotAllowed);
            }

            var secondPrice = PriceOf(secondHalf, sizeId);
            basePrice = halfAndHalfPricing switch
            {
                HalfAndHalfPricing.MoreExpensiveHalf => Math.Max(basePrice, secondPrice),
                HalfAndHalfPricing.AverageOfHalves => Money.Round((basePrice + secondPrice) / 2),
                _ => throw new ArgumentOutOfRangeException(nameof(halfAndHalfPricing)),
            };

            // Whole-item modifiers must make sense for both halves.
            allowedGroupIds.IntersectWith(secondHalf.ModifierGroups.Select(g => g.ModifierGroupId));
        }

        var groups = modifierGroups.Where(g => allowedGroupIds.Contains(g.Id)).ToList();
        if (modifierIds.Distinct().Count() != modifierIds.Count)
        {
            throw new DomainException(MenuErrors.ModifierUnavailable);
        }

        var chosen = modifierIds.Select(id => PriceModifier(groups, id, sizeId)).ToList();

        foreach (var group in groups)
        {
            var count = chosen.Count(m => m.ModifierGroupId == group.Id);
            if (count < group.MinSelect || count > group.MaxSelect)
            {
                throw new DomainException(
                    MenuErrors.ModifierSelectionInvalid,
                    $"Modifier group {group.Id} needs {group.MinSelect}-{group.MaxSelect} choices, got {count}.");
            }
        }

        return new PricedItem(basePrice, chosen);
    }

    private static decimal PriceOf(Product product, Guid sizeId)
    {
        if (!product.IsActive)
        {
            throw new DomainException(MenuErrors.ProductUnavailable);
        }

        return product.PriceFor(sizeId) ?? throw new DomainException(MenuErrors.SizeUnavailable);
    }

    private static PricedModifier PriceModifier(List<ModifierGroup> groups, Guid modifierId, Guid sizeId)
    {
        foreach (var group in groups)
        {
            if (group.FindModifier(modifierId) is { IsActive: true } modifier
                && modifier.PriceFor(sizeId) is { } price)
            {
                return new PricedModifier(modifier.Id, group.Id, modifier.Name, price);
            }
        }

        throw new DomainException(MenuErrors.ModifierUnavailable);
    }
}
