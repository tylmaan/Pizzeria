namespace Pizzeria.LocalAgent.Printing;

// JSON contract with the API (Pizzeria.Application/Printing/PrintPayloads.cs): change both sides together.
// The agent does not reference the API projects, so it can be deployed and updated on its own.

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

/// <summary>A job as delivered by the hub. The kind stays a string, so an agent older than the API skips unknown kinds.</summary>
public sealed record AgentJob(Guid Id, string Kind, string Payload);

public static class PrintJobKinds
{
    public const string KitchenTicket = nameof(KitchenTicket);
    public const string FiscalReceipt = nameof(FiscalReceipt);
}
