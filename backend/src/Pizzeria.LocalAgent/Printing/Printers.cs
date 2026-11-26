namespace Pizzeria.LocalAgent.Printing;

/// <summary>The printer could not print (no paper, offline, rejected command). The message is shown in the panel.</summary>
public sealed class PrinterException(string message) : Exception(message);

/// <summary>Kitchen ticket printer (ESC/POS). Real drivers come later; the simulator writes files.</summary>
public interface IReceiptPrinter
{
    /// <returns><c>null</c> when no printer is configured.</returns>
    Task<bool?> CheckOnlineAsync(CancellationToken ct);

    Task PrintKitchenTicketAsync(KitchenTicketPayload ticket, CancellationToken ct);
}

/// <summary>Fiscal printer. Protocols (Posnet, Novitus, Elzab) come later; the simulator writes files.</summary>
public interface IFiscalPrinter
{
    /// <returns><c>null</c> when no printer is configured.</returns>
    Task<bool?> CheckOnlineAsync(CancellationToken ct);

    Task PrintReceiptAsync(FiscalReceiptPayload receipt, CancellationToken ct);
}

public sealed class NoReceiptPrinter : IReceiptPrinter
{
    public Task<bool?> CheckOnlineAsync(CancellationToken ct) => Task.FromResult<bool?>(null);

    public Task PrintKitchenTicketAsync(KitchenTicketPayload ticket, CancellationToken ct) =>
        throw new PrinterException("No kitchen ticket printer configured");
}

public sealed class NoFiscalPrinter : IFiscalPrinter
{
    public Task<bool?> CheckOnlineAsync(CancellationToken ct) => Task.FromResult<bool?>(null);

    public Task PrintReceiptAsync(FiscalReceiptPayload receipt, CancellationToken ct) =>
        throw new PrinterException("No fiscal printer configured");
}
