using System.Text.Json;
using Pizzeria.Domain.Orders;
using Pizzeria.Domain.Printing;

namespace Pizzeria.Application.Printing;

// JSON contract with the local agent (Pizzeria.LocalAgent/Printing/PrintPayloads.cs mirrors these records):
// change both sides together.

public sealed record KitchenTicketLine(int Quantity, string Name, string Size, IReadOnlyList<string> Modifiers, string? Notes);

public sealed record KitchenTicketPayload(
    int Number,
    DateTimeOffset PlacedAt,
    string Fulfillment,
    IReadOnlyList<KitchenTicketLine> Lines,
    string? Notes);

public sealed record FiscalReceiptLine(string Name, int Quantity, decimal UnitPrice, decimal Total);

public sealed record FiscalReceiptPayload(
    int Number,
    IReadOnlyList<FiscalReceiptLine> Lines,
    decimal DeliveryFee,
    decimal Total,
    string PaymentMethod);

public static class PrintPayloads
{
    public static string For(PrintJobKind kind, Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var items = order.Items.OrderBy(i => i.SortOrder).ToList();
        static string NameOf(OrderItem i) => i.SecondHalfProductName is null ? i.ProductName : $"{i.ProductName} / {i.SecondHalfProductName}";

        object payload = kind switch
        {
            PrintJobKind.KitchenTicket => new KitchenTicketPayload(
                order.Number,
                order.PlacedAt,
                order.Fulfillment.ToString(),
                [.. items.Select(i => new KitchenTicketLine(i.Quantity, NameOf(i), i.SizeName, [.. i.Modifiers.Select(m => m.Name)], i.Notes))],
                order.Notes),
            PrintJobKind.FiscalReceipt => new FiscalReceiptPayload(
                order.Number,
                [.. items.Select(i => new FiscalReceiptLine($"{NameOf(i)} {i.SizeName}", i.Quantity, i.UnitPrice, i.LineTotal))],
                order.DeliveryFee,
                order.Total,
                order.PaymentMethod.ToString()),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        return JsonSerializer.Serialize(payload, JsonSerializerOptions.Web);
    }
}
