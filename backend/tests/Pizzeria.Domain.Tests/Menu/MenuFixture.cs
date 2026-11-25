using Pizzeria.Domain.Menu;

namespace Pizzeria.Domain.Tests.Menu;

/// <summary>
/// Pizza category (32/42 cm) with Margherita 25/32, Pepperoni 30/38, Calzone 33/– (no halves),
/// "Toppings" 0–3 (cheese 4/6, mushrooms 3/5, inactive pineapple) and "Sauce" exactly 1 (garlic 2/2, tomato 2/3).
/// </summary>
internal sealed class MenuFixture
{
    public static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    public MenuFixture()
    {
        Pizza = Category.Create(CompanyId, "Pizza", 0, ["32 cm", "42 cm"], Now);
        Small = Pizza.Sizes[0].Id;
        Large = Pizza.Sizes[1].Id;

        Toppings = ModifierGroup.Create(Pizza, "Dodatki", 0, 3, 0,
        [
            new(null, "Ser", true, Prices(4m, 6m)),
            new(null, "Pieczarki", true, Prices(3m, 5m)),
            new(null, "Ananas", false, Prices(3m, 5m)),
        ], Now);
        Sauce = ModifierGroup.Create(Pizza, "Sos", 1, 1, 1,
        [
            new(null, "Czosnkowy", true, Prices(2m, 2m)),
            new(null, "Pomidorowy", true, Prices(2m, 3m)),
        ], Now);

        Margherita = Product.Create(Pizza, "Margherita", null, true, 0, Prices(25m, 32m), [Toppings, Sauce], Now);
        Pepperoni = Product.Create(Pizza, "Pepperoni", null, true, 1, Prices(30m, 38m), [Toppings, Sauce], Now);
        Calzone = Product.Create(Pizza, "Calzone", null, false, 2, new Dictionary<Guid, decimal> { [Small] = 33m }, [Sauce], Now);
    }

    public Guid CompanyId { get; } = Guid.NewGuid();

    public Category Pizza { get; }

    public Guid Small { get; }

    public Guid Large { get; }

    public ModifierGroup Toppings { get; }

    public ModifierGroup Sauce { get; }

    public Product Margherita { get; }

    public Product Pepperoni { get; }

    public Product Calzone { get; }

    public IReadOnlyCollection<ModifierGroup> Groups => [Toppings, Sauce];

    public static Guid Modifier(ModifierGroup group, string name) => group.Modifiers.Single(m => m.Name == name).Id;

    public Dictionary<Guid, decimal> Prices(decimal small, decimal large) => new() { [Small] = small, [Large] = large };
}
