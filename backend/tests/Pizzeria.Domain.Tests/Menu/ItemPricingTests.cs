using Pizzeria.Domain.Common;
using Pizzeria.Domain.Menu;

namespace Pizzeria.Domain.Tests.Menu;

public sealed class ItemPricingTests
{
    private readonly MenuFixture _menu = new();

    private Guid Garlic => MenuFixture.Modifier(_menu.Sauce, "Czosnkowy");

    private Guid Tomato => MenuFixture.Modifier(_menu.Sauce, "Pomidorowy");

    private Guid Cheese => MenuFixture.Modifier(_menu.Toppings, "Ser");

    private Guid Mushrooms => MenuFixture.Modifier(_menu.Toppings, "Pieczarki");

    [Fact]
    public void Price_UsesPriceOfChosenSize()
    {
        var small = Price(_menu.Margherita, null, _menu.Small, Garlic);
        var large = Price(_menu.Margherita, null, _menu.Large, Garlic);

        Assert.Equal(25m, small.BasePrice);
        Assert.Equal(32m, large.BasePrice);
    }

    [Fact]
    public void Price_AddsModifierPricesForTheSize()
    {
        var item = Price(_menu.Margherita, null, _menu.Large, Tomato, Cheese, Mushrooms);

        Assert.Equal(32m, item.BasePrice);
        Assert.Equal([3m, 6m, 5m], item.Modifiers.Select(m => m.Price));
        Assert.Equal(46m, item.UnitPrice);
    }

    [Fact]
    public void Price_ReturnsModifierSnapshotData()
    {
        var item = Price(_menu.Margherita, null, _menu.Small, Garlic);

        var modifier = Assert.Single(item.Modifiers);
        Assert.Equal(("Czosnkowy", _menu.Sauce.Id, 2m), (modifier.Name, modifier.ModifierGroupId, modifier.Price));
    }

    [Theory]
    [InlineData(HalfAndHalfPricing.MoreExpensiveHalf, 38)]
    [InlineData(HalfAndHalfPricing.AverageOfHalves, 35)]
    public void Price_HalfAndHalf_FollowsCompanyRule(HalfAndHalfPricing rule, decimal expected)
    {
        var item = ItemPricing.Price(_menu.Margherita, _menu.Pepperoni, _menu.Large, _menu.Groups, [Garlic], rule);

        Assert.Equal(expected, item.BasePrice);
    }

    [Fact]
    public void Price_HalfAndHalfAverage_RoundsHalfGroszAwayFromZero()
    {
        var odd = Product.Create(_menu.Pizza, "Wege", null, true, 3, _menu.Prices(25.01m, 30m), [_menu.Sauce], MenuFixture.Now);

        var item = ItemPricing.Price(_menu.Margherita, odd, _menu.Small, _menu.Groups, [Garlic], HalfAndHalfPricing.AverageOfHalves);

        Assert.Equal(25.01m, item.BasePrice); // (25.00 + 25.01) / 2 = 25.005 → 25.01
    }

    [Fact]
    public void Price_HalfAndHalf_IsTheSameWhicheverHalfComesFirst()
    {
        var a = ItemPricing.Price(_menu.Margherita, _menu.Pepperoni, _menu.Small, _menu.Groups, [Garlic], HalfAndHalfPricing.MoreExpensiveHalf);
        var b = ItemPricing.Price(_menu.Pepperoni, _menu.Margherita, _menu.Small, _menu.Groups, [Garlic], HalfAndHalfPricing.MoreExpensiveHalf);

        Assert.Equal(a.UnitPrice, b.UnitPrice);
    }

    [Fact]
    public void Price_HalfAndHalf_WithProductThatDoesNotAllowHalves_Throws()
    {
        AssertFails(MenuErrors.HalvesNotAllowed, () => Price(_menu.Margherita, _menu.Calzone, _menu.Small, Garlic));
    }

    [Fact]
    public void Price_HalfAndHalf_OnlyOffersGroupsSharedByBothHalves()
    {
        var plain = Product.Create(_menu.Pizza, "Biała", null, true, 3, _menu.Prices(26m, 33m), [_menu.Sauce], MenuFixture.Now);

        AssertFails(MenuErrors.ModifierUnavailable, () => Price(_menu.Margherita, plain, _menu.Small, Garlic, Cheese));
    }

    [Fact]
    public void Price_SizeNotOffered_Throws()
    {
        AssertFails(MenuErrors.SizeUnavailable, () => Price(_menu.Calzone, null, _menu.Large, Garlic));
    }

    [Fact]
    public void Price_HalfAndHalf_SecondHalfNotOfferedInSize_Throws()
    {
        var smallOnly = Product.Create(_menu.Pizza, "Mini", null, true, 3, new Dictionary<Guid, decimal> { [_menu.Small] = 20m }, [_menu.Sauce], MenuFixture.Now);

        AssertFails(MenuErrors.SizeUnavailable, () => Price(_menu.Margherita, smallOnly, _menu.Large, Garlic));
    }

    [Fact]
    public void Price_InactiveProduct_Throws()
    {
        _menu.Pepperoni.Update(_menu.Pizza, "Pepperoni", null, true, isActive: false, _menu.Prices(30m, 38m), [_menu.Toppings, _menu.Sauce]);

        AssertFails(MenuErrors.ProductUnavailable, () => Price(_menu.Pepperoni, null, _menu.Small, Garlic));
    }

    [Fact]
    public void Price_RequiredGroupWithoutChoice_Throws()
    {
        AssertFails(MenuErrors.ModifierSelectionInvalid, () => Price(_menu.Margherita, null, _menu.Small, Cheese));
    }

    [Fact]
    public void Price_TooManyChoicesInGroup_Throws()
    {
        AssertFails(MenuErrors.ModifierSelectionInvalid, () => Price(_menu.Margherita, null, _menu.Small, Garlic, Tomato));
    }

    [Fact]
    public void Price_InactiveModifier_Throws()
    {
        var pineapple = MenuFixture.Modifier(_menu.Toppings, "Ananas");

        AssertFails(MenuErrors.ModifierUnavailable, () => Price(_menu.Margherita, null, _menu.Small, Garlic, pineapple));
    }

    [Fact]
    public void Price_ModifierOfGroupNotAttachedToProduct_Throws()
    {
        AssertFails(MenuErrors.ModifierUnavailable, () => Price(_menu.Calzone, null, _menu.Small, Garlic, Cheese));
    }

    [Fact]
    public void Price_SameModifierTwice_Throws()
    {
        AssertFails(MenuErrors.ModifierUnavailable, () => Price(_menu.Margherita, null, _menu.Small, Garlic, Cheese, Cheese));
    }

    [Fact]
    public void Price_UnknownModifier_Throws()
    {
        AssertFails(MenuErrors.ModifierUnavailable, () => Price(_menu.Margherita, null, _menu.Small, Garlic, Guid.NewGuid()));
    }

    private PricedItem Price(Product product, Product? secondHalf, Guid sizeId, params Guid[] modifiers) =>
        ItemPricing.Price(product, secondHalf, sizeId, _menu.Groups, modifiers, HalfAndHalfPricing.MoreExpensiveHalf);

    private static void AssertFails(string code, Func<object> act)
    {
        var ex = Assert.Throws<DomainException>(act);
        Assert.Equal(code, ex.Code);
    }
}
