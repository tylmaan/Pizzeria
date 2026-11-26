using Pizzeria.Domain.Common;

namespace Pizzeria.Domain.Printing;

public enum PrintJobKind
{
    /// <summary>Ticket for the kitchen (ESC/POS receipt printer).</summary>
    KitchenTicket,

    /// <summary>Fiscal receipt (fiscal printer). When to print it is a location setting.</summary>
    FiscalReceipt,
}

public enum PrintJobStatus
{
    /// <summary>Waiting for the agent (also while the agent is offline).</summary>
    Pending,

    /// <summary>Delivered to the agent, not confirmed yet.</summary>
    Sent,

    Printed,

    Failed,
}

/// <summary>
/// A document to print in a location. Jobs wait while the agent is offline and are delivered when it reconnects;
/// sent but unconfirmed jobs are delivered again after a reconnect.
/// </summary>
public sealed class PrintJob : ILocationEntity
{
    public const int ErrorMaxLength = 500;

    private PrintJob()
    {
    }

    public Guid Id { get; private set; }

    public Guid CompanyId { get; private set; }

    public Guid LocationId { get; private set; }

    public Guid? OrderId { get; private set; }

    public PrintJobKind Kind { get; private set; }

    public PrintJobStatus Status { get; private set; }

    /// <summary>What to print, as JSON (rendered to ESC/POS or fiscal commands by the agent).</summary>
    public string Payload { get; private set; } = null!;

    public int Attempts { get; private set; }

    public string? Error { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public DateTimeOffset? PrintedAt { get; private set; }

    public static PrintJob Create(Guid companyId, Guid locationId, Guid? orderId, PrintJobKind kind, string payload, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        return new PrintJob
        {
            Id = Guid.CreateVersion7(now),
            CompanyId = companyId,
            LocationId = locationId,
            OrderId = orderId,
            Kind = kind,
            Status = PrintJobStatus.Pending,
            Payload = payload,
            CreatedAt = now,
        };
    }

    /// <summary>Handed over to the agent. Counts as an attempt.</summary>
    public void MarkSent(DateTimeOffset now)
    {
        if (Status is PrintJobStatus.Printed or PrintJobStatus.Failed)
        {
            return;
        }

        Status = PrintJobStatus.Sent;
        SentAt = now;
        Attempts++;
    }

    /// <summary>Confirmation from the agent. Idempotent.</summary>
    public void MarkPrinted(DateTimeOffset now)
    {
        if (Status == PrintJobStatus.Printed)
        {
            return;
        }

        Status = PrintJobStatus.Printed;
        PrintedAt = now;
        Error = null;
    }

    /// <summary>The printer reported an error (no paper, offline). Staff can retry from the panel.</summary>
    public void MarkFailed(string error)
    {
        if (Status == PrintJobStatus.Printed)
        {
            return;
        }

        Status = PrintJobStatus.Failed;
        var trimmed = string.IsNullOrWhiteSpace(error) ? "unknown" : error.Trim();
        Error = trimmed[..Math.Min(trimmed.Length, ErrorMaxLength)];
    }

    public void Retry()
    {
        if (Status != PrintJobStatus.Failed)
        {
            throw new DomainException(PrintingErrors.JobNotRetryable);
        }

        Status = PrintJobStatus.Pending;
        Error = null;
    }
}

/// <summary>When the fiscal receipt of an order is printed. A location setting, to be confirmed with the accountant.</summary>
public enum FiscalReceiptTrigger
{
    /// <summary>Not printed automatically (default until the rules are confirmed).</summary>
    Never,

    /// <summary>When the order is accepted.</summary>
    OnAccepted,

    /// <summary>When the order is handed over (delivered or picked up).</summary>
    OnCompleted,
}
