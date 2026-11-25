using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pizzeria.Application.Abstractions;
using Pizzeria.Application.Errors;
using Pizzeria.Application.Tenancy;
using Pizzeria.Domain.Menu;

namespace Pizzeria.Application.Menu;

/// <summary>
/// The company menu. Every query goes through the tenant-filtered context, so ids of another company's
/// categories, products or groups are simply "not found". Only company admins may change the menu.
/// </summary>
public sealed class MenuService(
    IAppDbContext db,
    ITenantContext tenant,
    TimeProvider clock,
    IValidator<SaveCategoryRequest> categoryValidator,
    IValidator<SaveProductRequest> productValidator,
    IValidator<SaveModifierGroupRequest> groupValidator)
{
    public async Task<MenuDto> GetAsync(CancellationToken ct)
    {
        var company = await db.Companies.AsNoTracking().SingleOrDefaultAsync(ct).ConfigureAwait(false)
            ?? throw new ForbiddenException();
        var categories = await db.Categories.AsNoTracking().OrderBy(c => c.SortOrder).ToListAsync(ct).ConfigureAwait(false);
        var products = await db.Products.AsNoTracking()
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .ToListAsync(ct).ConfigureAwait(false);
        var groups = await db.ModifierGroups.AsNoTracking()
            .OrderBy(g => g.SortOrder).ThenBy(g => g.Name)
            .ToListAsync(ct).ConfigureAwait(false);

        return new MenuDto(
            company.HalfAndHalfPricing,
            categories.ConvertAll(CategoryDto.From),
            products.ConvertAll(ProductDto.From),
            groups.ConvertAll(ModifierGroupDto.From));
    }

    public async Task<MenuSettingsDto> UpdateSettingsAsync(MenuSettingsDto request, CancellationToken ct)
    {
        RequireCompanyAdmin();
        var company = await db.Companies.SingleAsync(ct).ConfigureAwait(false);
        company.SetHalfAndHalfPricing(request.HalfAndHalfPricing);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return new MenuSettingsDto(company.HalfAndHalfPricing);
    }

    public async Task<CategoryDto> CreateCategoryAsync(SaveCategoryRequest request, CancellationToken ct)
    {
        RequireCompanyAdmin();
        await categoryValidator.ValidateAndThrowAsync(request, ct).ConfigureAwait(false);

        var sortOrder = await db.Categories.CountAsync(ct).ConfigureAwait(false);
        var category = Category.Create(
            tenant.CompanyId!.Value,
            request.Name,
            sortOrder,
            [.. request.Sizes.Select(s => s.Name)],
            clock.GetUtcNow());

        db.Categories.Add(category);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return CategoryDto.From(category);
    }

    public async Task<CategoryDto> UpdateCategoryAsync(Guid id, SaveCategoryRequest request, CancellationToken ct)
    {
        RequireCompanyAdmin();
        await categoryValidator.ValidateAndThrowAsync(request, ct).ConfigureAwait(false);

        var category = await FindCategoryAsync(id, ct).ConfigureAwait(false);
        category.Rename(request.Name);
        var removed = category.UpdateSizes([.. request.Sizes.Select(s => new SizeDefinition(s.Id, s.Name))]);

        // Prices reference sizes without a database FK, so a size still in use must not disappear.
        if (removed.Count > 0)
        {
            var usedByProduct = await db.Products
                .AnyAsync(p => p.Prices.Any(x => removed.Contains(x.SizeId)), ct).ConfigureAwait(false);
            var usedByModifier = await db.ModifierGroups
                .AnyAsync(g => g.Modifiers.Any(m => m.Prices.Any(x => removed.Contains(x.SizeId))), ct).ConfigureAwait(false);
            if (usedByProduct || usedByModifier)
            {
                throw new ConflictException(ErrorCodes.SizeInUse);
            }
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return CategoryDto.From(category);
    }

    public async Task<ProductDto> CreateProductAsync(SaveProductRequest request, CancellationToken ct)
    {
        RequireCompanyAdmin();
        await productValidator.ValidateAndThrowAsync(request, ct).ConfigureAwait(false);

        var category = await FindCategoryAsync(request.CategoryId, ct).ConfigureAwait(false);
        var groups = await FindGroupsAsync(request.ModifierGroupIds, ct).ConfigureAwait(false);
        var sortOrder = await db.Products.CountAsync(p => p.CategoryId == category.Id, ct).ConfigureAwait(false);

        var product = Product.Create(
            category,
            request.Name,
            request.Description,
            request.AllowsHalves,
            sortOrder,
            ToPrices(request.Prices),
            groups,
            clock.GetUtcNow());
        if (!request.IsActive)
        {
            product.Update(category, request.Name, request.Description, request.AllowsHalves, false, ToPrices(request.Prices), groups);
        }

        db.Products.Add(product);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return ProductDto.From(product);
    }

    public async Task<ProductDto> UpdateProductAsync(Guid id, SaveProductRequest request, CancellationToken ct)
    {
        RequireCompanyAdmin();
        await productValidator.ValidateAndThrowAsync(request, ct).ConfigureAwait(false);

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct).ConfigureAwait(false)
            ?? throw new NotFoundException(ErrorCodes.ProductNotFound);
        var category = await FindCategoryAsync(request.CategoryId, ct).ConfigureAwait(false);
        var groups = await FindGroupsAsync(request.ModifierGroupIds, ct).ConfigureAwait(false);

        product.Update(category, request.Name, request.Description, request.AllowsHalves, request.IsActive, ToPrices(request.Prices), groups);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return ProductDto.From(product);
    }

    public async Task<ModifierGroupDto> CreateModifierGroupAsync(SaveModifierGroupRequest request, CancellationToken ct)
    {
        RequireCompanyAdmin();
        await groupValidator.ValidateAndThrowAsync(request, ct).ConfigureAwait(false);

        var category = await FindCategoryAsync(request.CategoryId, ct).ConfigureAwait(false);
        var sortOrder = await db.ModifierGroups.CountAsync(g => g.CategoryId == category.Id, ct).ConfigureAwait(false);
        var group = ModifierGroup.Create(
            category,
            request.Name,
            request.MinSelect,
            request.MaxSelect,
            sortOrder,
            ToModifiers(request.Modifiers),
            clock.GetUtcNow());

        db.ModifierGroups.Add(group);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return ModifierGroupDto.From(group);
    }

    public async Task<ModifierGroupDto> UpdateModifierGroupAsync(Guid id, SaveModifierGroupRequest request, CancellationToken ct)
    {
        RequireCompanyAdmin();
        await groupValidator.ValidateAndThrowAsync(request, ct).ConfigureAwait(false);

        var group = await db.ModifierGroups.FirstOrDefaultAsync(g => g.Id == id, ct).ConfigureAwait(false)
            ?? throw new NotFoundException(ErrorCodes.ModifierGroupNotFound);
        var category = await FindCategoryAsync(request.CategoryId, ct).ConfigureAwait(false);

        group.Update(category, request.Name, request.MinSelect, request.MaxSelect, ToModifiers(request.Modifiers));
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return ModifierGroupDto.From(group);
    }

    private async Task<Category> FindCategoryAsync(Guid id, CancellationToken ct) =>
        await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct).ConfigureAwait(false)
        ?? throw new NotFoundException(ErrorCodes.CategoryNotFound);

    private async Task<List<ModifierGroup>> FindGroupsAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToList();
        var groups = await db.ModifierGroups.Where(g => distinct.Contains(g.Id)).ToListAsync(ct).ConfigureAwait(false);
        if (groups.Count != distinct.Count)
        {
            throw new NotFoundException(ErrorCodes.ModifierGroupNotFound);
        }

        // Keep the order chosen by the admin.
        return [.. distinct.Select(id => groups.First(g => g.Id == id))];
    }

    private static Dictionary<Guid, decimal> ToPrices(IReadOnlyList<PriceDto> prices) =>
        prices.GroupBy(p => p.SizeId).ToDictionary(g => g.Key, g => g.Last().Price);

    private static List<ModifierDefinition> ToModifiers(IReadOnlyList<ModifierInput> modifiers) =>
        [.. modifiers.Select(m => new ModifierDefinition(m.Id, m.Name, m.IsActive, ToPrices(m.Prices)))];

    private void RequireCompanyAdmin()
    {
        if (!tenant.IsCompanyAdmin)
        {
            throw new ForbiddenException();
        }
    }
}
