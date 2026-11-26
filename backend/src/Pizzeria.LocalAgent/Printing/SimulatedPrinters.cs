using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;

namespace Pizzeria.LocalAgent.Printing;

public sealed class SimulatorOptions
{
    /// <summary>Where printouts are written (relative paths: next to the agent executable).</summary>
    public string OutputDirectory { get; set; } = "print-output";
}

/// <summary>Writes each printout to a text file instead of a printer.</summary>
public sealed partial class SimulatedPrinter(IOptions<SimulatorOptions> options, TimeProvider clock, ILogger<SimulatedPrinter> logger)
    : IReceiptPrinter, IFiscalPrinter
{
    public Task<bool?> CheckOnlineAsync(CancellationToken ct) => Task.FromResult<bool?>(true);

    public Task PrintKitchenTicketAsync(KitchenTicketPayload ticket, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        return WriteAsync($"kitchen-{ticket.Number}", TicketFormatter.KitchenTicket(ticket, clock.LocalTimeZone), ct);
    }

    public Task PrintReceiptAsync(FiscalReceiptPayload receipt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return WriteAsync($"receipt-{receipt.Number}", TicketFormatter.FiscalReceipt(receipt), ct);
    }

    private async Task WriteAsync(string name, string text, CancellationToken ct)
    {
        // Relative to the executable: a Windows service runs with System32 as its working directory.
        var directory = Path.GetFullPath(options.Value.OutputDirectory, AppContext.BaseDirectory);
        Directory.CreateDirectory(directory);
        var stamp = clock.GetLocalNow().ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        var path = Path.Combine(directory, $"{stamp}-{name}.txt");
        await File.WriteAllTextAsync(path, text, Encoding.UTF8, ct).ConfigureAwait(false);
        LogPrinted(logger, path);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Simulated printout written to {Path}")]
    private static partial void LogPrinted(ILogger logger, string path);
}
