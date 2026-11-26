using Pizzeria.Domain.Orders;

namespace Pizzeria.Domain.Printing;

/// <summary>Which documents an order needs when it reaches a status, according to the location's settings.</summary>
public static class PrintRules
{
    public static IReadOnlyList<PrintJobKind> DueOn(OrderStatus newStatus, bool printKitchenTickets, FiscalReceiptTrigger fiscalTrigger)
    {
        List<PrintJobKind> due = [];

        if (newStatus == OrderStatus.Accepted && printKitchenTickets)
        {
            due.Add(PrintJobKind.KitchenTicket);
        }

        var fiscalNow = fiscalTrigger switch
        {
            FiscalReceiptTrigger.OnAccepted => newStatus == OrderStatus.Accepted,
            FiscalReceiptTrigger.OnCompleted => newStatus is OrderStatus.Delivered or OrderStatus.PickedUp,
            _ => false,
        };
        if (fiscalNow)
        {
            due.Add(PrintJobKind.FiscalReceipt);
        }

        return due;
    }
}
