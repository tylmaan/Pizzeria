using Pizzeria.Domain.Menu;

namespace Pizzeria.Application.Menu;

public sealed record PriceDto(Guid SizeId, decimal Price);

// Owned collections come back from the database in no particular order: always sort by SortOrder.

public sealed record SizeDto(Guid Id, string Name);

public sealed record CategoryDto(Guid Id, string Name, IReadOnlyList<SizeDto> Sizes)
{
    public static CategoryDto From(Category c) => new(c.Id, c.Name, [.. c.Sizes.OrderBy(s => s.SortOrder).Select(s => new SizeDto(s.Id, s.Name))]);
}

public sealed record ProductDto(
    Guid Id,
    Guid CategoryId,
    string Name,
    string? Description,
    bool IsActive,
    bool AllowsHalves,
    IReadOnlyList<PriceDto> Prices,
    IReadOnlyList<Guid> ModifierGroupIds)
{
    public static ProductDto From(Product p) => new(
        p.Id,
        p.CategoryId,
        p.Name,
        p.Description,
        p.IsActive,
        p.AllowsHalves,
        [.. p.Prices.Select(x => new PriceDto(x.SizeId, x.Price))],
        [.. p.ModifierGroups.OrderBy(g => g.SortOrder).Select(g => g.ModifierGroupId)]);
}

public sealed record ModifierDto(Guid Id, string Name, bool IsActive, IReadOnlyList<PriceDto> Prices);

public sealed record ModifierGroupDto(
    Guid Id,
    Guid CategoryId,
    string Name,
    int MinSelect,
    int MaxSelect,
    IReadOnlyList<ModifierDto> Modifiers)
{
    public static ModifierGroupDto From(ModifierGroup g) => new(
        g.Id,
        g.CategoryId,
        g.Name,
        g.MinSelect,
        g.MaxSelect,
        [.. g.Modifiers.OrderBy(m => m.SortOrder).Select(m => new ModifierDto(m.Id, m.Name, m.IsActive, [.. m.Prices.Select(x => new PriceDto(x.SizeId, x.Price))]))]);
}

/// <summary>The whole company menu as edited in the company panel (and read by staff for phone orders).</summary>
public sealed record MenuDto(
    HalfAndHalfPricing HalfAndHalfPricing,
    IReadOnlyList<CategoryDto> Categories,
    IReadOnlyList<ProductDto> Products,
    IReadOnlyList<ModifierGroupDto> ModifierGroups);

public sealed record SizeInput(Guid? Id, string Name);

public sealed record SaveCategoryRequest(string Name, IReadOnlyList<SizeInput> Sizes);

public sealed record SaveProductRequest(
    Guid CategoryId,
    string Name,
    string? Description,
    bool AllowsHalves,
    bool IsActive,
    IReadOnlyList<PriceDto> Prices,
    IReadOnlyList<Guid> ModifierGroupIds);

public sealed record ModifierInput(Guid? Id, string Name, bool IsActive, IReadOnlyList<PriceDto> Prices);

public sealed record SaveModifierGroupRequest(
    Guid CategoryId,
    string Name,
    int MinSelect,
    int MaxSelect,
    IReadOnlyList<ModifierInput> Modifiers);

public sealed record MenuSettingsDto(HalfAndHalfPricing HalfAndHalfPricing);
