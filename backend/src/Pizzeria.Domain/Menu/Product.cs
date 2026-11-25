using Pizzeria.Domain.Common;

namespace Pizzeria.Domain.Menu;

/// <summary>Price of a product in one size of its category (a product variant, e.g. Margherita 42 cm).</summary>
public sealed class ProductPrice
{
    private ProductPrice()
    {
    }

    internal ProductPrice(Guid sizeId, decimal price)
    {
        SizeId = sizeId;
        Price = price;
    }

    public Guid SizeId { get; private set; }

    public decimal Price { get; internal set; }
}

/// <summary>A modifier group offered with the product.</summary>
public sealed class ProductModifierGroup
{
    private ProductModifierGroup()
    {
    }

    internal ProductModifierGroup(Guid modifierGroupId, int sortOrder)
    {
        ModifierGroupId = modifierGroupId;
        SortOrder = sortOrder;
    }

    public Guid ModifierGroupId { get; private set; }

    public int SortOrder { get; internal set; }
}

/// <summary>A menu item of a company. Offered in the sizes it has a price for.</summary>
public sealed class Product : ITenantEntity
{
    public const int NameMaxLength = 150;
    public const int DescriptionMaxLength = 500;

    private readonly List<ProductPrice> _prices = [];
    private readonly List<ProductModifierGroup> _modifierGroups = [];

    private Product()
    {
    }

    public Guid Id { get; private set; }

    public Guid CompanyId { get; private set; }

    public Guid CategoryId { get; private set; }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public int SortOrder { get; private set; }

    /// <summary>Inactive products stay in the menu editor but cannot be ordered.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Can be ordered as one half of a half-and-half pizza.</summary>
    public bool AllowsHalves { get; private set; }

    public IReadOnlyList<ProductPrice> Prices => _prices;

    public IReadOnlyList<ProductModifierGroup> ModifierGroups => _modifierGroups;

    public static Product Create(
        Category category,
        string name,
        string? description,
        bool allowsHalves,
        int sortOrder,
        IReadOnlyDictionary<Guid, decimal> prices,
        IReadOnlyList<ModifierGroup> modifierGroups,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(category);

        var product = new Product
        {
            Id = Guid.CreateVersion7(now),
            CompanyId = category.CompanyId,
            SortOrder = sortOrder,
        };
        product.Update(category, name, description, allowsHalves, isActive: true, prices, modifierGroups);
        return product;
    }

    public void Update(
        Category category,
        string name,
        string? description,
        bool allowsHalves,
        bool isActive,
        IReadOnlyDictionary<Guid, decimal> prices,
        IReadOnlyList<ModifierGroup> modifierGroups)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(modifierGroups);

        if (category.CompanyId != CompanyId || modifierGroups.Any(g => g.CategoryId != category.Id))
        {
            throw new DomainException(MenuErrors.CategoryMismatch);
        }

        var trimmedDescription = description?.Trim();
        if (trimmedDescription?.Length > DescriptionMaxLength)
        {
            throw new DomainException(MenuErrors.InvalidName);
        }

        Name = MenuNames.Require(name, NameMaxLength);
        Description = string.IsNullOrEmpty(trimmedDescription) ? null : trimmedDescription;
        CategoryId = category.Id;
        AllowsHalves = allowsHalves;
        IsActive = isActive;
        PriceList.Apply(_prices, prices, category, (sizeId, price) => new ProductPrice(sizeId, price), p => p.SizeId, (p, price) => p.Price = price);

        var groupIds = modifierGroups.Select(g => g.Id).Distinct().ToList();
        _modifierGroups.RemoveAll(g => !groupIds.Contains(g.ModifierGroupId));
        for (var i = 0; i < groupIds.Count; i++)
        {
            var existing = _modifierGroups.Find(g => g.ModifierGroupId == groupIds[i]);
            if (existing is null)
            {
                _modifierGroups.Add(new ProductModifierGroup(groupIds[i], i));
            }
            else
            {
                existing.SortOrder = i;
            }
        }

        _modifierGroups.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
    }

    public decimal? PriceFor(Guid sizeId) => _prices.Find(p => p.SizeId == sizeId)?.Price;
}
