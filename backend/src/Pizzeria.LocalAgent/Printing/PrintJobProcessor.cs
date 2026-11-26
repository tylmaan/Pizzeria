using System.Text.Json;

namespace Pizzeria.LocalAgent.Printing;

public sealed record PrintResult(bool Printed, string? Error)
{
    public static readonly PrintResult Ok = new(true, null);

    public static PrintResult Failed(string error) => new(false, error);
}

/// <summary>Prints one job on the right printer. Never throws for printer or payload problems: they become a failed job.</summary>
public sealed partial class PrintJobProcessor(IReceiptPrinter receiptPrinter, IFiscalPrinter fiscalPrinter, ILogger<PrintJobProcessor> logger)
{
    public async Task<PrintResult> ProcessAsync(AgentJob job, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(job);

        try
        {
            switch (job.Kind)
            {
                case PrintJobKinds.KitchenTicket:
                    await receiptPrinter.PrintKitchenTicketAsync(Read<KitchenTicketPayload>(job.Payload), ct).ConfigureAwait(false);
                    break;
                case PrintJobKinds.FiscalReceipt:
                    await fiscalPrinter.PrintReceiptAsync(Read<FiscalReceiptPayload>(job.Payload), ct).ConfigureAwait(false);
                    break;
                default:
                    return PrintResult.Failed($"Unsupported job kind: {job.Kind} (update the agent)");
            }

            return PrintResult.Ok;
        }
        catch (PrinterException ex)
        {
            LogPrintFailed(logger, job.Id, ex.Message);
            return PrintResult.Failed(ex.Message);
        }
        catch (JsonException ex)
        {
            LogPrintFailed(logger, job.Id, ex.Message);
            return PrintResult.Failed("Invalid print data");
        }
    }

    private static T Read<T>(string payload) =>
        JsonSerializer.Deserialize<T>(payload, JsonSerializerOptions.Web) ?? throw new JsonException("Empty payload");

    [LoggerMessage(Level = LogLevel.Warning, Message = "Print job {JobId} failed: {Error}")]
    private static partial void LogPrintFailed(ILogger logger, Guid jobId, string error);
}
