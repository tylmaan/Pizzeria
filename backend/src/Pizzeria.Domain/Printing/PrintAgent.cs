using System.Security.Cryptography;
using System.Text;
using Pizzeria.Domain.Common;

namespace Pizzeria.Domain.Printing;

public static class PrintingErrors
{
    public const string InvalidName = "printing.invalid_name";
    public const string AgentRevoked = "printing.agent_revoked";
    public const string JobNotRetryable = "printing.job_not_retryable";
}

/// <summary>
/// The local agent installed on the restaurant's computer (Windows service). It connects outbound to the API
/// and authenticates with a device token; only a hash of the token is stored.
/// </summary>
public sealed class PrintAgent : ILocationEntity
{
    public const int NameMaxLength = 100;

    /// <summary>The agent counts as online when it reported within this time.</summary>
    public static readonly TimeSpan OnlineWindow = TimeSpan.FromSeconds(90);

    private PrintAgent()
    {
    }

    public Guid Id { get; private set; }

    public Guid CompanyId { get; private set; }

    public Guid LocationId { get; private set; }

    public string Name { get; private set; } = null!;

    /// <summary>SHA-256 of the device token (hex). The token itself is shown once, when the agent is registered.</summary>
    public string TokenHash { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastSeenAt { get; private set; }

    /// <summary>Kitchen ticket printer status as last reported by the agent.</summary>
    public bool? ReceiptPrinterOnline { get; private set; }

    public bool? FiscalPrinterOnline { get; private set; }

    public bool IsRevoked { get; private set; }

    /// <returns>The agent and its device token (to configure the agent; not retrievable later).</returns>
    public static (PrintAgent Agent, string Token) Register(Guid companyId, Guid locationId, string name, DateTimeOffset now)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > NameMaxLength)
        {
            throw new DomainException(PrintingErrors.InvalidName);
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var agent = new PrintAgent
        {
            Id = Guid.CreateVersion7(now),
            CompanyId = companyId,
            LocationId = locationId,
            Name = trimmed,
            TokenHash = HashToken(token),
            CreatedAt = now,
        };
        return (agent, token);
    }

    public static string HashToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token ?? string.Empty)));

    public bool IsOnline(DateTimeOffset now) => !IsRevoked && LastSeenAt is { } seen && now - seen <= OnlineWindow;

    /// <summary>Heartbeat from the agent with the status of its printers.</summary>
    public void ReportStatus(bool? receiptPrinterOnline, bool? fiscalPrinterOnline, DateTimeOffset now)
    {
        if (IsRevoked)
        {
            throw new DomainException(PrintingErrors.AgentRevoked);
        }

        LastSeenAt = now;
        ReceiptPrinterOnline = receiptPrinterOnline;
        FiscalPrinterOnline = fiscalPrinterOnline;
    }

    /// <summary>Stolen or replaced computer: the token stops working immediately.</summary>
    public void Revoke() => IsRevoked = true;
}
