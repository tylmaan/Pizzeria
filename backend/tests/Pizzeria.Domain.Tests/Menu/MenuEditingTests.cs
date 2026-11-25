using Pizzeria.Domain.Common;
using Pizzeria.Domain.Menu;

namespace Pizzeria.Domain.Tests.Menu;

public sealed class MenuEditingTests
{
    private readonly MenuFixture _menu = new();

    [Fact]
    public void UpdateSizes_RenamesKeepsIdsAddsAndReportsRemoved()
    {
        var removed = _menu.Pizza.UpdateSizes([new(_menu.Large, "45 cm"), new(null, "60 cm")]);

        Assert.Equal([_menu.Small], removed);
        Assert.Equal(["45 cm", "60 cm"], _menu.Pizza.Sizes.Select(s => s.Name));
        Assert.Equal(_menu.Large, _menu.Pizza.Sizes[0].Id);
    }

    [Theory]
    [InlineData(new string[0], MenuErrors.SizesRequired)]
    [InlineData(new[] { "32 cm", "32 CM" }, MenuErrors.DuplicateSizeName)]
    [InlineData(new[] { " " }, MenuErrors.InvalidName)]
    public void Create_InvalidSizes_Throws(string[] sizes, string code)
    {
        var ex = Assert.Throws<DomainException>(() => Category.Create(_menu.CompanyId, "Pizza", 0, sizes, MenuFixture.Now));

        Assert.Equal(code, ex.Code);
    }

    [Fact]
    public void UpdateSizes_UnknownSizeId_Throws()
    {
        var ex = Assert.Throws<DomainException>(() => _menu.Pizza.UpdateSizes([new(Guid.NewGuid(), "32 cm")]));

        Assert.Equal(MenuErrors.UnknownSize, ex.Code);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10.001)]
    public void Product_InvalidPrice_Throws(decimal price)
    {
        var ex = Assert.Throws<DomainException>(() =>
            Product.Create(_menu.Pizza, "Test", null, false, 0, new Dictionary<Guid, decimal> { [_menu.Small] = price }, [], MenuFixture.Now));

        Assert.Equal(MenuErrors.InvalidPrice, ex.Code);
    }

    [Fact]
    public void Product_PriceInSizeOfAnotherCategory_Throws()
    {
        var drinks = Category.Create(_menu.CompanyId, "Napoje", 1, ["0,5 l"], MenuFixture.Now);

        var ex = Assert.Throws<DomainException>(() =>
            Product.Create(drinks, "Cola", null, false, 0, new Dictionary<Guid, decimal> { [_menu.Small] = 7m }, [], MenuFixture.Now));

        Assert.Equal(MenuErrors.UnknownSize, ex.Code);
    }

    [Fact]
    public void Product_WithoutPrices_Throws()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Product.Create(_menu.Pizza, "Test", null, false, 0, new Dictionary<Guid, decimal>(), [], MenuFixture.Now));

        Assert.Equal(MenuErrors.PricesRequired, ex.Code);
    }

    [Fact]
    public void Product_ModifierGroupOfAnotherCategory_Throws()
    {
        var drinks = Category.Create(_menu.CompanyId, "Napoje", 1, ["0,5 l"], MenuFixture.Now);

        var ex = Assert.Throws<DomainException>(() =>
            Product.Create(drinks, "Cola", null, false, 0, new Dictionary<Guid, decimal> { [drinks.Sizes[0].Id] = 7m }, [_menu.Sauce], MenuFixture.Now));

        Assert.Equal(MenuErrors.CategoryMismatch, ex.Code);
    }

    [Fact]
    public void Product_Update_ChangesPricesInPlaceAndDropsUnlistedSizes()
    {
        var originalPrice = _menu.Margherita.Prices.Single(p => p.SizeId == _menu.Small);

        _menu.Margherita.Update(_menu.Pizza, "Margherita", " Sos, ser ", true, true,
            new Dictionary<Guid, decimal> { [_menu.Small] = 26.5m }, [_menu.Sauce]);

        Assert.Same(originalPrice, Assert.Single(_menu.Margherita.Prices));
        Assert.Equal(26.5m, originalPrice.Price);
        Assert.Equal("Sos, ser", _menu.Margherita.Description);
        Assert.Equal([_menu.Sauce.Id], _menu.Margherita.ModifierGroups.Select(g => g.ModifierGroupId));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 3)] // more required than modifiers in the group
    public void ModifierGroup_InvalidLimits_Throws(int min, int max)
    {
        var ex = Assert.Throws<DomainException>(() => ModifierGroup.Create(_menu.Pizza, "Sos", min, max, 0,
        [
            new(null, "Czosnkowy", true, _menu.Prices(2m, 2m)),
            new(null, "Pomidorowy", true, _menu.Prices(2m, 2m)),
        ], MenuFixture.Now));

        Assert.Equal(MenuErrors.InvalidSelectionLimits, ex.Code);
    }

    [Fact]
    public void ModifierGroup_Update_KeepsIdsOfListedModifiersAndRemovesOthers()
    {
        var garlic = MenuFixture.Modifier(_menu.Sauce, "Czosnkowy");

        _menu.Sauce.Update(_menu.Pizza, "Sosy", 0, 2,
        [
            new(null, "BBQ", true, _menu.Prices(3m, 4m)),
            new(garlic, "Czosnkowy XL", true, _menu.Prices(2.5m, 3m)),
        ]);

        Assert.Equal(["BBQ", "Czosnkowy XL"], _menu.Sauce.Modifiers.Select(m => m.Name));
        Assert.Equal(garlic, _menu.Sauce.Modifiers[1].Id);
        Assert.Equal(3m, _menu.Sauce.Modifiers[1].PriceFor(_menu.Large));
    }

    [Fact]
    public void ModifierGroup_UpdateWithOtherCategory_Throws()
    {
        var drinks = Category.Create(_menu.CompanyId, "Napoje", 1, ["0,5 l"], MenuFixture.Now);

        var ex = Assert.Throws<DomainException>(() => _menu.Sauce.Update(drinks, "Sos", 1, 1,
            [new(null, "Lód", true, new Dictionary<Guid, decimal> { [drinks.Sizes[0].Id] = 1m })]));

        Assert.Equal(MenuErrors.CategoryMismatch, ex.Code);
    }
}
