using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pizzeria.Application.Abstractions;
using Pizzeria.Application.Errors;
using Pizzeria.Application.Tenancy;
using Pizzeria.Domain.Identity;
using Pizzeria.Domain.Orders;
using Pizzeria.Domain.Printing;

namespace Pizzeria.Application.Printing;

public sealed record PrintAgentDto(
    Guid Id,
    Guid LocationId,
    string Name,
    bool IsOnline,
    DateTimeOffset? LastSeenAt,
    bool? ReceiptPrinterOnline,
    bool? FiscalPrinterOnline,
    bool IsRevoked)
{
    public static PrintAgentDto From(PrintAgent a, DateTimeOffset now) =>
        new(a.Id, a.LocationId, a.Name, a.IsOnline(now), a.LastSeenAt, a.ReceiptPrinterOnline, a.FiscalPrinterOnline, a.IsRevoked);
}

/// <summary>A newly registered agent with its device token — shown once, to put into the agent configuration.</summary>
public sealed record RegisteredAgentDto(PrintAgentDto Agent, string Token);

public sealed record RegisterAgentRequest(string Name);

public sealed record PrintJobDto(Guid Id, Guid? OrderId, PrintJobKind Kind, PrintJobStatus Status, int Attempts, string? Error, DateTimeOffset CreatedAt, DateTimeOffset? PrintedAt)
{
    public static PrintJobDto From(PrintJob j) => new(j.Id, j.OrderId, j.Kind, j.Status, j.Attempts, j.Error, j.CreatedAt, j.PrintedAt);
}

public sealed record PrintSettingsDto(bool PrintKitchenTickets, FiscalReceiptTrigger FiscalReceiptTrigger);

/// <summary>A job as delivered to the agent.</summary>
public sealed record AgentJobDto(Guid Id, PrintJobKind Kind, string Payload);

/// <summary>Wakes up the agents of a location when new jobs are waiting (they then fetch them).</summary>
public interface IPrintJobNotifier
{
    Task JobsAvailableAsync(Guid locationId, CancellationToken ct);
}

public sealed class NullPrintJobNotifier : IPrintJobNotifier
{
    public Task JobsAvailableAsync(Guid locationId, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>Creates print jobs when orders change status, following the location's settings.</summary>
public sealed class PrintJobScheduler(IAppDbContext db, TimeProvider clock, IPrintJobNotifier notifier)
{
    /// <summary>Call after the order's new status was saved.</summary>
    public async Task ScheduleAsync(Order order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);

        var location = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == order.LocationId, ct).ConfigureAwait(false);
        var due = PrintRules.DueOn(order.Status, location.PrintKitchenTickets, location.FiscalReceiptTrigger);
        if (due.Count == 0)
        {
            return;
        }

        // Without an agent nobody could ever print these: do not pile up jobs.
        var hasAgent = await db.PrintAgents.AnyAsync(a => a.LocationId == order.LocationId && !a.IsRevoked, ct).ConfigureAwait(false);
        if (!hasAgent)
        {
            return;
        }

        var now = clock.GetUtcNow();
        foreach (var kind in due)
        {
            db.PrintJobs.Add(PrintJob.Create(order.CompanyId, order.LocationId, order.Id, kind, PrintPayloads.For(kind, order), now));
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await notifier.JobsAvailableAsync(order.LocationId, ct).ConfigureAwait(false);
    }
}

/// <summary>Agents, print jobs and print settings of a location (company admins and managers).</summary>
public sealed class PrintingService(
    IAppDbContext db,
    ITenantContext tenant,
    TimeProvider clock,
    IPrintJobNotifier notifier,
    IValidator<RegisterAgentRequest> registerValidator,
    IValidator<PrintSettingsDto> settingsValidator)
{
    public async Task<RegisteredAgentDto> RegisterAgentAsync(Guid locationId, RegisterAgentRequest request, CancellationToken ct)
    {
        await RequireManagerOfAsync(locationId, ct).ConfigureAwait(false);
        await registerValidator.ValidateAndThrowAsync(request, ct).ConfigureAwait(false);
        var (agent, token) = PrintAgent.Register(tenant.CompanyId!.Value, locationId, request.Name, clock.GetUtcNow());
        db.PrintAgents.Add(agent);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return new RegisteredAgentDto(PrintAgentDto.From(agent, clock.GetUtcNow()), token);
    }

    public async Task<IReadOnlyList<PrintAgentDto>> ListAgentsAsync(Guid locationId, CancellationToken ct)
    {
        await RequireStaffOfAsync(locationId, ct).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        var agents = await db.PrintAgents.AsNoTracking().Where(a => a.LocationId == locationId).OrderBy(a => a.Name).ToListAsync(ct).ConfigureAwait(false);
        return agents.ConvertAll(a => PrintAgentDto.From(a, now));
    }

    public async Task RevokeAgentAsync(Guid agentId, CancellationToken ct)
    {
        var agent = await db.PrintAgents.FirstOrDefaultAsync(a => a.Id == agentId, ct).ConfigureAwait(false)
            ?? throw new NotFoundException(ErrorCodes.AgentNotFound);
        await RequireManagerOfAsync(agent.LocationId, ct).ConfigureAwait(false);
        agent.Revoke();
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PrintJobDto>> ListJobsAsync(Guid locationId, CancellationToken ct)
    {
        await RequireStaffOfAsync(locationId, ct).ConfigureAwait(false);
        var jobs = await db.PrintJobs.AsNoTracking()
            .Where(j => j.LocationId == locationId)
            .OrderByDescending(j => j.CreatedAt)
            .Take(50)
            .ToListAsync(ct).ConfigureAwait(false);
        return jobs.ConvertAll(PrintJobDto.From);
    }

    public async Task<PrintJobDto> RetryJobAsync(Guid jobId, CancellationToken ct)
    {
        var job = await db.PrintJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct).ConfigureAwait(false)
            ?? throw new NotFoundException(ErrorCodes.PrintJobNotFound);
        await RequireStaffOfAsync(job.LocationId, ct).ConfigureAwait(false);
        job.Retry();
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await notifier.JobsAvailableAsync(job.LocationId, ct).ConfigureAwait(false);
        return PrintJobDto.From(job);
    }

    /// <summary>Prints the kitchen ticket of an order again (e.g. the first one was lost).</summary>
    public async Task<PrintJobDto> ReprintTicketAsync(Guid orderId, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, ct).ConfigureAwait(false)
            ?? throw new NotFoundException(ErrorCodes.OrderNotFound);
        await RequireStaffOfAsync(order.LocationId, ct).ConfigureAwait(false);

        var job = PrintJob.Create(order.CompanyId, order.LocationId, order.Id, PrintJobKind.KitchenTicket,
            PrintPayloads.For(PrintJobKind.KitchenTicket, order), clock.GetUtcNow());
        db.PrintJobs.Add(job);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await notifier.JobsAvailableAsync(order.LocationId, ct).ConfigureAwait(false);
        return PrintJobDto.From(job);
    }

    public async Task<PrintSettingsDto> UpdateSettingsAsync(Guid locationId, PrintSettingsDto request, CancellationToken ct)
    {
        await RequireManagerOfAsync(locationId, ct).ConfigureAwait(false);
        await settingsValidator.ValidateAndThrowAsync(request, ct).ConfigureAwait(false);
        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == locationId, ct).ConfigureAwait(false)
            ?? throw new NotFoundException(ErrorCodes.LocationNotFound);
        location.SetPrinting(request.PrintKitchenTickets, request.FiscalReceiptTrigger);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return new PrintSettingsDto(location.PrintKitchenTickets, location.FiscalReceiptTrigger);
    }

    public async Task<PrintSettingsDto> GetSettingsAsync(Guid locationId, CancellationToken ct)
    {
        await RequireStaffOfAsync(locationId, ct).ConfigureAwait(false);
        var location = await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId, ct).ConfigureAwait(false)
            ?? throw new NotFoundException(ErrorCodes.LocationNotFound);
        return new PrintSettingsDto(location.PrintKitchenTickets, location.FiscalReceiptTrigger);
    }

    private Task RequireManagerOfAsync(Guid locationId, CancellationToken ct) =>
        RequireLocationAsync(tenant.IsCompanyAdmin || tenant.IsInRole(Roles.LocationManager), locationId, ct);

    private Task RequireStaffOfAsync(Guid locationId, CancellationToken ct) =>
        RequireLocationAsync(tenant.IsCompanyAdmin || tenant.IsInRole(Roles.LocationManager) || tenant.IsInRole(Roles.Staff), locationId, ct);

    /// <summary>The location must be accessible and belong to the current company (tenant-filtered lookup).</summary>
    private async Task RequireLocationAsync(bool allowedRole, Guid locationId, CancellationToken ct)
    {
        if (!allowedRole || !tenant.CanAccessLocation(locationId)
            || !await db.Locations.AnyAsync(l => l.Id == locationId, ct).ConfigureAwait(false))
        {
            throw new NotFoundException(ErrorCodes.LocationNotFound);
        }
    }
}

/// <summary>An agent identified by its device token.</summary>
public sealed record AgentIdentity(Guid AgentId, Guid CompanyId, Guid LocationId, string Name);

/// <summary>Finds agents by device token. Runs before any tenant is known.</summary>
public sealed class AgentAuthenticationService(IAppDbContext db)
{
    public async Task<AgentIdentity?> FindActiveAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var hash = PrintAgent.HashToken(token);

        // Tenant filter bypassed on purpose: the token is the credential and itself determines the company.
        var agent = await db.PrintAgents.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(a => a.TokenHash == hash && !a.IsRevoked, ct).ConfigureAwait(false);
        return agent is null ? null : new AgentIdentity(agent.Id, agent.CompanyId, agent.LocationId, agent.Name);
    }
}

/// <summary>What the connected agent does: fetch jobs, confirm them, report status. The tenant is the agent's location.</summary>
public sealed class AgentSessionService(IAppDbContext db, ITenantContext tenant, TimeProvider clock)
{
    /// <summary>Sent jobs not confirmed within this time are delivered again (the agent may have crashed).</summary>
    public static readonly TimeSpan ResendAfter = TimeSpan.FromMinutes(2);

    public async Task<IReadOnlyList<AgentJobDto>> FetchJobsAsync(CancellationToken ct)
    {
        var agent = await RequireActiveAgentAsync(ct).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        var staleBefore = now - ResendAfter;

        var jobs = await db.PrintJobs
            .Where(j => j.LocationId == agent.LocationId
                        && (j.Status == PrintJobStatus.Pending || (j.Status == PrintJobStatus.Sent && j.SentAt < staleBefore)))
            .OrderBy(j => j.CreatedAt)
            .Take(20)
            .ToListAsync(ct).ConfigureAwait(false);
        foreach (var job in jobs)
        {
            job.MarkSent(now);
        }

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another agent of the location took (some of) these jobs at the same moment: let it print them.
            return [];
        }

        return jobs.ConvertAll(j => new AgentJobDto(j.Id, j.Kind, j.Payload));
    }

    public async Task ConfirmAsync(Guid jobId, bool printed, string? error, CancellationToken ct)
    {
        var agent = await RequireActiveAgentAsync(ct).ConfigureAwait(false);
        var job = await db.PrintJobs.FirstOrDefaultAsync(j => j.Id == jobId && j.LocationId == agent.LocationId, ct).ConfigureAwait(false)
            ?? throw new NotFoundException(ErrorCodes.PrintJobNotFound);
        if (printed)
        {
            job.MarkPrinted(clock.GetUtcNow());
        }
        else
        {
            job.MarkFailed(error ?? string.Empty);
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task HeartbeatAsync(bool? receiptPrinterOnline, bool? fiscalPrinterOnline, CancellationToken ct)
    {
        var agent = await RequireActiveAgentAsync(ct).ConfigureAwait(false);
        agent.ReportStatus(receiptPrinterOnline, fiscalPrinterOnline, clock.GetUtcNow());
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>The connection outlives the token check at connect time: a revoked agent is cut off on its next call.</summary>
    private async Task<PrintAgent> RequireActiveAgentAsync(CancellationToken ct)
    {
        if (!tenant.IsInRole(Roles.Agent) || tenant.UserId is not { } agentId)
        {
            throw new ForbiddenException();
        }

        var agent = await db.PrintAgents.FirstOrDefaultAsync(a => a.Id == agentId, ct).ConfigureAwait(false);
        return agent is { IsRevoked: false } && tenant.CanAccessLocation(agent.LocationId)
            ? agent
            : throw new ForbiddenException(PrintingErrors.AgentRevoked);
    }
}
