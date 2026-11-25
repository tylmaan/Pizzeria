namespace Pizzeria.LocalAgent;

/// <summary>
/// Placeholder for the local agent loop. It will connect outbound to the API's AgentHub
/// and execute print jobs (fiscal receipts, kitchen tickets) — see CLAUDE.md, stage 2.
/// </summary>
public sealed partial class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(logger);
        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Local agent started (no hub connection yet)")]
    private static partial void LogStarted(ILogger logger);
}
