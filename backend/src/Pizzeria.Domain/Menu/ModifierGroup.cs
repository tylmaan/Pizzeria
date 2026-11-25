using Pizzeria.Domain.Common;

namespace Pizzeria.Domain.Menu;

public sealed class ModifierPrice
{
    private ModifierPrice()
    {
    }

    internal ModifierPrice(Guid sizeId, decimal price)
    {
        SizeId = sizeId;
        Price = price;
    }

    public Guid SizeId { get; private set; }

    public decimal Price { get; internal set; }
}

/// <summary>One option of a modifier group, e.g. "Extra cheese". Priced per size of the group's category.</summary>
public sealed class Modifier
{
    private readonly List<ModifierPrice> _prices = [];

    private Modifier()
    {
    }

    internal Modifier(Guid id)
    {
        Id = id;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyList<ModifierPrice> Prices => _prices;

    public decimal? PriceFor(Guid sizeId) => _prices.Find(p => p.SizeId == sizeId)?.Price;

    internal void Apply(ModifierDefinition definition, int sortOrder, Category category)
    {
        Name = MenuNames.Require(definition.Name, ModifierGroup.NameMaxLength);
        SortOrder = sortOrder;
        IsActive = definition.IsActive;
        PriceList.Apply(_prices, definition.Prices, category, (sizeId, price) => new ModifierPrice(sizeId, price), p => p.SizeId, (p, price) => p.Price = price);
    }
}

/// <summary>Desired state of a modifier when editing a group: <see cref="Id"/> is null for a new modifier.</summary>
public sealed record ModifierDefinition(Guid? Id, string Name, bool IsActive, IReadOnlyDictionary<Guid, decimal> Prices);

/// <summary>
/// A choice attached to products, e.g. "Extra toppings" (0–5) or "Sauce" (exactly 1).
/// Belongs to one category, because modifier prices depend on that category's sizes.
/// </summary>
public sealed class ModifierGroup : ITenantEntity
{
    public const int NameMaxLength = 100;
    public const int MaxModifiers = 50;

    private readonly List<Modifier> _modifiers = [];

    private ModifierGroup()
    {
    }

    public Guid Id { get; private set; }

    public Guid CompanyId { get; private set; }

    public Guid CategoryId { get; private set; }

    public string Name { get; private set; } = null!;

    /// <summary>Minimum number of modifiers the customer has to pick (0 = optional).</summary>
    public int MinSelect { get; private set; }

    public int MaxSelect { get; private set; }

    public int SortOrder { get; private set; }

    public IReadOnlyList<Modifier> Modifiers => _modifiers;

    public static ModifierGroup Create(
        Category category,
        string name,
        int minSelect,
        int maxSelect,
        int sortOrder,
        IReadOnlyList<ModifierDefinition> modifiers,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(category);

        var group = new ModifierGroup
        {
            Id = Guid.CreateVersion7(now),
            CompanyId = category.CompanyId,
            CategoryId = category.Id,
            SortOrder = sortOrder,
        };
        group.Update(category, name, minSelect, maxSelect, modifiers);
        return group;
    }

    /// <param name="category">The group's own category (needed to validate size prices).</param>
    public void Update(Category category, string name, int minSelect, int maxSelect, IReadOnlyList<ModifierDefinition> modifiers)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(modifiers);

        if (category.Id != CategoryId)
        {
            throw new DomainException(MenuErrors.CategoryMismatch);
        }

        if (modifiers.Count == 0 || modifiers.Count > MaxModifiers
            || minSelect < 0 || maxSelect < 1 || minSelect > maxSelect || minSelect > modifiers.Count)
        {
            throw new DomainException(MenuErrors.InvalidSelectionLimits);
        }

        if (modifiers.Any(m => m.Id is { } id && !_modifiers.Exists(x => x.Id == id)))
        {
            throw new DomainException(MenuErrors.UnknownModifier);
        }

        Name = MenuNames.Require(name, NameMaxLength);
        MinSelect = minSelect;
        MaxSelect = maxSelect;

        var kept = modifiers.Where(m => m.Id is not null).Select(m => m.Id!.Value).ToHashSet();
        _modifiers.RemoveAll(m => !kept.Contains(m.Id));
        for (var i = 0; i < modifiers.Count; i++)
        {
            var modifier = modifiers[i].Id is { } id ? _modifiers.First(m => m.Id == id) : AddModifier();
            modifier.Apply(modifiers[i], i, category);
        }

        _modifiers.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
    }

    public Modifier? FindModifier(Guid modifierId) => _modifiers.Find(m => m.Id == modifierId);

    private Modifier AddModifier()
    {
        var modifier = new Modifier(Guid.CreateVersion7());
        _modifiers.Add(modifier);
        return modifier;
    }
}

/// <summary>Shared logic for "price per size" lists of products and modifiers.</summary>
internal static class PriceList
{
    /// <summary>Makes <paramref name="prices"/> match <paramref name="desired"/>, updating entries in place.</summary>
    public static void Apply<T>(
        List<T> prices,
        IReadOnlyDictionary<Guid, decimal> desired,
        Category category,
        Func<Guid, decimal, T> create,
        Func<T, Guid> sizeOf,
        Action<T, decimal> setPrice)
    {
        ArgumentNullException.ThrowIfNull(desired);

        if (desired.Count == 0)
        {
            throw new DomainException(MenuErrors.PricesRequired);
        }

        if (desired.Keys.Any(sizeId => !category.HasSize(sizeId)))
        {
            throw new DomainException(MenuErrors.UnknownSize);
        }

        foreach (var price in desired.Values)
        {
            Money.RequirePrice(price);
        }

        prices.RemoveAll(p => !desired.ContainsKey(sizeOf(p)));
        foreach (var (sizeId, price) in desired)
        {
            var existing = prices.Find(p => sizeOf(p) == sizeId);
            if (existing is null)
            {
                prices.Add(create(sizeId, price));
            }
            else
            {
                setPrice(existing, price);
            }
        }
    }
}
