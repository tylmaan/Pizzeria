using Microsoft.AspNetCore.SignalR;
using Pizzeria.Application.Errors;
using Pizzeria.Application.Printing;
using Pizzeria.Application.Tenancy;
using Pizzeria.Domain.Identity;

namespace Pizzeria.Api.Realtime;

/// <summary>
/// Connection of the local print agent (outbound from the restaurant, so no open ports there).
/// The server only says "jobs are waiting"; the agent fetches them, prints and confirms each one.
/// Group: <c>agent:{locationId}</c>.
/// </summary>
public sealed class AgentHub(ITenantContext tenant, AgentSessionService session) : Hub
{
    public const string Path = "/hubs/agent";

    /// <summary>Server → agent: new jobs are waiting, call <see cref="FetchJobs"/>.</summary>
    public const string JobsAvailable = nameof(JobsAvailable);

    public static string LocationGroup(Guid locationId) => $"agent:{locationId}";

    public override async Task OnConnectedAsync()
    {
        if (!tenant.IsInRole(Roles.Agent) || tenant.LocationIds.Count != 1)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, LocationGroup(tenant.LocationIds.First())).ConfigureAwait(false);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    public Task<IReadOnlyList<AgentJobDto>> FetchJobs() =>
        CutOffIfRevoked(() => session.FetchJobsAsync(Context.ConnectionAborted));

    public Task Confirm(Guid jobId, bool printed, string? error) =>
        CutOffIfRevoked(async () =>
        {
            await session.ConfirmAsync(jobId, printed, error, Context.ConnectionAborted).ConfigureAwait(false);
            return true;
        });

    public Task Heartbeat(bool? receiptPrinterOnline, bool? fiscalPrinterOnline) =>
        CutOffIfRevoked(async () =>
        {
            await session.HeartbeatAsync(receiptPrinterOnline, fiscalPrinterOnline, Context.ConnectionAborted).ConfigureAwait(false);
            return true;
        });

    /// <summary>A revoked agent loses its connection; reconnecting then fails authentication.</summary>
    private async Task<T> CutOffIfRevoked<T>(Func<Task<T>> call)
    {
        try
        {
            return await call().ConfigureAwait(false);
        }
        catch (ForbiddenException)
        {
            Context.Abort();
            throw new HubException("forbidden");
        }
    }
}

public sealed class SignalRPrintJobNotifier(IHubContext<AgentHub> hub) : IPrintJobNotifier
{
    public Task JobsAvailableAsync(Guid locationId, CancellationToken ct) =>
        hub.Clients.Group(AgentHub.LocationGroup(locationId)).SendAsync(AgentHub.JobsAvailable, ct);
}
