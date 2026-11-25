using Pizzeria.Domain.Common;
using Pizzeria.Domain.Menu;

namespace Pizzeria.Domain.Orders;

/// <summary>Modifier as it was when the order was placed (later menu changes do not affect it).</summary>
public sealed class OrderItemModifier
{
    private OrderItemModifier()
    {
    }

    internal OrderItemModifier(Guid modifierId, string name, decimal price)
    {
        Id = Guid.CreateVersion7();
        ModifierId = modifierId;
        Name = name;
        Price = price;
    }

    public Guid Id { get; private set; }

    public Guid ModifierId { get; private set; }

    public string Name { get; private set; } = null!;

    public decimal Price { get; private set; }
}

/// <summary>What the customer chose, with names to show and the price computed by <see cref="ItemPricing"/>.</summary>
public sealed record OrderLine(
    Guid ProductId,
    string ProductName,
    Guid? SecondHalfProductId,
    string? SecondHalfProductName,
    Guid SizeId,
    string SizeName,
    int Quantity,
    PricedItem Price,
    string? Notes);

/// <summary>
/// An order line frozen at the moment of ordering: names, size, modifiers and prices are copied,
/// so changing or removing a menu item never changes past orders.
/// </summary>
public sealed class OrderItem
{
    public const int MaxQuantity = 99;
    public const int NotesMaxLength = 200;

    private readonly List<OrderItemModifier> _modifiers = [];

    private OrderItem()
    {
    }

    public Guid Id { get; private set; }

    public Guid ProductId { get; private set; }

    public string ProductName { get; private set; } = null!;

    public Guid? SecondHalfProductId { get; private set; }

    public string? SecondHalfProductName { get; private set; }

    public Guid SizeId { get; private set; }

    public string SizeName { get; private set; } = null!;

    public int Quantity { get; private set; }

    /// <summary>Base price of one piece (for half-and-half: after applying the company rule).</summary>
    public decimal BasePrice { get; private set; }

    /// <summary>One piece including modifiers.</summary>
    public decimal UnitPrice { get; private set; }

    public decimal LineTotal { get; private set; }

    public string? Notes { get; private set; }

    public int SortOrder { get; private set; }

    public IReadOnlyList<OrderItemModifier> Modifiers => _modifiers;

    internal static OrderItem From(OrderLine line, int sortOrder)
    {
        if (line.Quantity is < 1 or > MaxQuantity)
        {
            throw new DomainException(OrderErrors.InvalidQuantity);
        }

        var notes = line.Notes?.Trim();
        var item = new OrderItem
        {
            Id = Guid.CreateVersion7(),
            ProductId = line.ProductId,
            ProductName = line.ProductName,
            SecondHalfProductId = line.SecondHalfProductId,
            SecondHalfProductName = line.SecondHalfProductName,
            SizeId = line.SizeId,
            SizeName = line.SizeName,
            Quantity = line.Quantity,
            BasePrice = line.Price.BasePrice,
            UnitPrice = line.Price.UnitPrice,
            LineTotal = line.Price.UnitPrice * line.Quantity,
            Notes = string.IsNullOrEmpty(notes) ? null : notes[..Math.Min(notes.Length, NotesMaxLength)],
            SortOrder = sortOrder,
        };
        item._modifiers.AddRange(line.Price.Modifiers.Select(m => new OrderItemModifier(m.ModifierId, m.Name, m.Price)));
        return item;
    }
}
