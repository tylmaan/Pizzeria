using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using Pizzeria.LocalAgent.Printing;

namespace Pizzeria.LocalAgent;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>Base address of the API, e.g. <c>https://api.example.com/</c>.</summary>
    public Uri? ApiUrl { get; set; }

    /// <summary>Device token shown once when the agent was registered in the panel. Secret: never commit it.</summary>
    public string Token { get; set; } = string.Empty;

    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Safety net: jobs are also fetched this often, in case a "jobs available" message was missed.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// Keeps an outbound SignalR connection to the API (no open ports in the restaurant), fetches waiting print jobs
/// when told so (and periodically), prints them and confirms each one. Reports printer status in heartbeats.
/// </summary>
public sealed partial class AgentWorker(
    IOptions<AgentOptions> options,
    PrintJobProcessor processor,
    IReceiptPrinter receiptPrinter,
    IFiscalPrinter fiscalPrinter,
    ILogger<AgentWorker> logger) : BackgroundService
{
    private const string HubPath = "hubs/agent";

    // One pending wake-up is enough: a fetch takes everything that is waiting.
    private readonly Channel<bool> _wakeUp = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (settings.ApiUrl is null || string.IsNullOrWhiteSpace(settings.Token))
        {
            LogNotConfigured(logger);
            return;
        }

        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(settings.ApiUrl, HubPath), o => o.AccessTokenProvider = () => Task.FromResult<string?>(settings.Token))
            .WithAutomaticReconnect(new KeepRetrying())
            .Build();
        connection.On("JobsAvailable", () => _wakeUp.Writer.TryWrite(true));
        connection.Reconnecting += error =>
        {
            LogDisconnected(logger, error?.Message);
            return Task.CompletedTask;
        };
        connection.Reconnected += _ =>
        {
            LogConnected(logger, settings.ApiUrl);
            _wakeUp.Writer.TryWrite(true); // jobs created while offline
            return Task.CompletedTask;
        };

        await ConnectAsync(connection, settings.ApiUrl, stoppingToken).ConfigureAwait(false);
        _wakeUp.Writer.TryWrite(true);

        var heartbeats = SendHeartbeatsAsync(connection, settings.HeartbeatInterval, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            await WaitForWorkAsync(settings.PollInterval, stoppingToken).ConfigureAwait(false);
            await PrintWaitingJobsAsync(connection, stoppingToken).ConfigureAwait(false);
        }

        await heartbeats.ConfigureAwait(false);
    }

    /// <summary>Automatic reconnect only covers connections that were up once: the first connect is retried here.</summary>
    private async Task ConnectAsync(HubConnection connection, Uri apiUrl, CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(2);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await connection.StartAsync(ct).ConfigureAwait(false);
                LogConnected(logger, apiUrl);
                return;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                LogUnauthorized(logger);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or TimeoutException or System.Net.WebSockets.WebSocketException)
            {
                LogConnectFailed(logger, ex.Message, delay);
            }

            await Task.Delay(delay, ct).ConfigureAwait(false);
            delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 60));
        }

        ct.ThrowIfCancellationRequested();
    }

    private async Task WaitForWorkAsync(TimeSpan pollInterval, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(pollInterval);
        try
        {
            await _wakeUp.Reader.ReadAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Poll interval elapsed.
        }
    }

    private async Task PrintWaitingJobsAsync(HubConnection connection, CancellationToken ct)
    {
        if (connection.State != HubConnectionState.Connected)
        {
            return;
        }

        try
        {
            while (true)
            {
                var jobs = await connection.InvokeAsync<List<AgentJob>>("FetchJobs", ct).ConfigureAwait(false);
                if (jobs.Count == 0)
                {
                    return;
                }

                foreach (var job in jobs)
                {
                    var result = await processor.ProcessAsync(job, ct).ConfigureAwait(false);
                    await connection.InvokeAsync("Confirm", job.Id, result.Printed, result.Error, ct).ConfigureAwait(false);
                    LogJobDone(logger, job.Id, job.Kind, result.Printed);
                }
            }
        }
        catch (Exception ex) when (ex is HubException or InvalidOperationException or IOException)
        {
            // Connection dropped or the server refused: unconfirmed jobs are delivered again later.
            LogFetchFailed(logger, ex.Message);
        }
    }

    private async Task SendHeartbeatsAsync(HubConnection connection, TimeSpan interval, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(interval);
        do
        {
            if (connection.State != HubConnectionState.Connected)
            {
                continue;
            }

            try
            {
                var receipt = await receiptPrinter.CheckOnlineAsync(ct).ConfigureAwait(false);
                var fiscal = await fiscalPrinter.CheckOnlineAsync(ct).ConfigureAwait(false);
                await connection.InvokeAsync("Heartbeat", receipt, fiscal, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HubException or InvalidOperationException or IOException)
            {
                LogFetchFailed(logger, ex.Message);
            }
        }
        while (await WaitAsync(timer, ct).ConfigureAwait(false));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>A restaurant computer may be offline for hours: never give up, back off to one attempt a minute.</summary>
    private sealed class KeepRetrying : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext) =>
            TimeSpan.FromSeconds(Math.Min(Math.Pow(2, Math.Min(retryContext.PreviousRetryCount, 6)), 60));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Agent:ApiUrl and Agent:Token must be configured (token from the panel: Ustawienia druku)")]
    private static partial void LogNotConfigured(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to {ApiUrl}")]
    private static partial void LogConnected(ILogger logger, Uri apiUrl);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Connection lost ({Reason}), reconnecting")]
    private static partial void LogDisconnected(ILogger logger, string? reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "The API rejected the agent token (revoked or mistyped). Register the agent again in the panel.")]
    private static partial void LogUnauthorized(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cannot connect to the API ({Reason}), retrying in {Delay}")]
    private static partial void LogConnectFailed(ILogger logger, string reason, TimeSpan delay);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Talking to the API failed: {Reason}")]
    private static partial void LogFetchFailed(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Job {JobId} ({Kind}) printed: {Printed}")]
    private static partial void LogJobDone(ILogger logger, Guid jobId, string kind, bool printed);
}
