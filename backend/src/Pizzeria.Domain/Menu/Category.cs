using Pizzeria.Domain.Common;

namespace Pizzeria.Domain.Menu;

/// <summary>Size of a category, e.g. "32 cm" for pizzas or "0,5 l" for drinks.</summary>
public sealed class Size
{
    private Size()
    {
    }

    internal Size(Guid id, string name, int sortOrder)
    {
        Id = id;
        Name = name;
        SortOrder = sortOrder;
    }

    public Guid Id { get; private set; }

    public string Name { get; internal set; } = null!;

    public int SortOrder { get; internal set; }
}

/// <summary>Desired state of a size when editing a category: <see cref="Id"/> is null for a new size.</summary>
public sealed record SizeDefinition(Guid? Id, string Name);

/// <summary>
/// Menu category of a company (pizzas, drinks...). Defines the sizes its products and modifier groups are priced in.
/// </summary>
public sealed class Category : ITenantEntity
{
    public const int NameMaxLength = 100;
    public const int SizeNameMaxLength = 30;
    public const int MaxSizes = 10;

    private readonly List<Size> _sizes = [];

    private Category()
    {
    }

    public Guid Id { get; private set; }

    public Guid CompanyId { get; private set; }

    public string Name { get; private set; } = null!;

    public int SortOrder { get; private set; }

    public IReadOnlyList<Size> Sizes => _sizes;

    public static Category Create(Guid companyId, string name, int sortOrder, IReadOnlyList<string> sizeNames, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(sizeNames);

        var category = new Category
        {
            Id = Guid.CreateVersion7(now),
            CompanyId = companyId,
            SortOrder = sortOrder,
        };
        category.Rename(name);
        category.UpdateSizes([.. sizeNames.Select(n => new SizeDefinition(null, n))]);
        return category;
    }

    public void Rename(string name) => Name = MenuNames.Require(name, NameMaxLength);

    public bool HasSize(Guid sizeId) => _sizes.Exists(s => s.Id == sizeId);

    /// <summary>
    /// Renames existing sizes, adds new ones and removes the ones not listed. Returns the removed size ids:
    /// the caller must make sure no product or modifier is still priced in them before saving.
    /// </summary>
    public IReadOnlyList<Guid> UpdateSizes(IReadOnlyList<SizeDefinition> sizes)
    {
        ArgumentNullException.ThrowIfNull(sizes);

        if (sizes.Count == 0)
        {
            throw new DomainException(MenuErrors.SizesRequired);
        }

        if (sizes.Count > MaxSizes)
        {
            throw new DomainException(MenuErrors.TooManySizes);
        }

        var names = sizes.Select(s => MenuNames.Require(s.Name, SizeNameMaxLength)).ToList();
        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count)
        {
            throw new DomainException(MenuErrors.DuplicateSizeName);
        }

        if (sizes.Any(s => s.Id is { } id && !HasSize(id)))
        {
            throw new DomainException(MenuErrors.UnknownSize);
        }

        var kept = sizes.Where(s => s.Id is not null).Select(s => s.Id!.Value).ToHashSet();
        var removed = _sizes.Where(s => !kept.Contains(s.Id)).Select(s => s.Id).ToList();
        _sizes.RemoveAll(s => !kept.Contains(s.Id));

        // Existing entities are updated in place so EF tracks renames as updates, not delete + insert.
        for (var i = 0; i < sizes.Count; i++)
        {
            if (sizes[i].Id is { } id)
            {
                var size = _sizes.First(s => s.Id == id);
                size.Name = names[i];
                size.SortOrder = i;
            }
            else
            {
                _sizes.Add(new Size(Guid.CreateVersion7(), names[i], i));
            }
        }

        _sizes.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        return removed;
    }
}

internal static class MenuNames
{
    public static string Require(string? value, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Length > maxLength)
        {
            throw new DomainException(MenuErrors.InvalidName);
        }

        return trimmed;
    }
}
