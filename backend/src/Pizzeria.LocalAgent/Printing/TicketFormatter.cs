using System.Globalization;
using System.Text;

namespace Pizzeria.LocalAgent.Printing;

/// <summary>
/// Plain-text layout of printouts (fixed width, as on an 80 mm printer). Real drivers send the same lines
/// wrapped in their own commands (ESC/POS, fiscal protocol).
/// </summary>
public static class TicketFormatter
{
    public const int Width = 42;

    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    public static string KitchenTicket(KitchenTicketPayload ticket, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var text = new StringBuilder();
        text.AppendLine(Center(" KUCHNIA ", '='));
        text.AppendLine(Columns($"Zamówienie #{ticket.Number}", FulfillmentLabel(ticket.Fulfillment)));
        text.AppendLine(TimeZoneInfo.ConvertTime(ticket.PlacedAt, timeZone).ToString("dd.MM.yyyy HH:mm", Polish));
        text.AppendLine(new string('-', Width));
        foreach (var line in ticket.Lines)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"{line.Quantity}x {line.Name} {line.Size}");
            foreach (var modifier in line.Modifiers)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"   + {modifier}");
            }

            if (!string.IsNullOrWhiteSpace(line.Notes))
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"   ! {line.Notes}");
            }
        }

        if (!string.IsNullOrWhiteSpace(ticket.Notes))
        {
            text.AppendLine(new string('-', Width));
            text.AppendLine(CultureInfo.InvariantCulture, $"Uwagi: {ticket.Notes}");
        }

        text.AppendLine(new string('=', Width));
        return text.ToString();
    }

    public static string FiscalReceipt(FiscalReceiptPayload receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        var text = new StringBuilder();
        text.AppendLine(Center(" PARAGON (SYMULATOR) ", '='));
        text.AppendLine(CultureInfo.InvariantCulture, $"Zamówienie #{receipt.Number}");
        text.AppendLine(new string('-', Width));
        foreach (var line in receipt.Lines)
        {
            text.AppendLine(line.Name);
            text.AppendLine(Columns($"  {line.Quantity} x {Money(line.UnitPrice)}", Money(line.Total)));
        }

        if (receipt.DeliveryFee > 0)
        {
            text.AppendLine(Columns("Dostawa", Money(receipt.DeliveryFee)));
        }

        text.AppendLine(new string('-', Width));
        text.AppendLine(Columns("SUMA PLN", Money(receipt.Total)));
        text.AppendLine(Columns("Płatność", receipt.PaymentMethod));
        text.AppendLine(new string('=', Width));
        return text.ToString();
    }

    private static string FulfillmentLabel(string fulfillment) => fulfillment switch
    {
        "Delivery" => "DOSTAWA",
        "Pickup" => "ODBIÓR",
        "DineIn" => "NA MIEJSCU",
        _ => fulfillment.ToUpperInvariant(),
    };

    private static string Money(decimal amount) => amount.ToString("0.00", Polish);

    private static string Columns(string left, string right)
    {
        var gap = Math.Max(1, Width - left.Length - right.Length);
        return left + new string(' ', gap) + right;
    }

    private static string Center(string title, char fill)
    {
        var left = Math.Max(0, (Width - title.Length) / 2);
        return (new string(fill, left) + title).PadRight(Width, fill);
    }
}
