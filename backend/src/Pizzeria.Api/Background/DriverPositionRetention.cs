using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pizzeria.Infrastructure.Persistence;

namespace Pizzeria.Api.Background;

public sealed class DriverTrackingOptions
{
    public const string SectionName = "DriverTracking";

    /// <summary>GPS trail is kept this long and then deleted (GDPR). Default 30 days.</summary>
    public int RetentionDays { get; set; } = 30;
}

/// <summary>Hourly job deleting driver positions older than the retention period, for all companies.</summary>
public sealed partial class DriverPositionRetention(
    IServiceScopeFactory scopes,
    IOptions<DriverTrackingOptions> options,
    TimeProvider clock,
    ILogger<DriverPositionRetention> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), clock);
        do
        {
            try
            {
                await DeleteExpiredAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    public async Task<int> DeleteExpiredAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cutoff = clock.GetUtcNow().AddDays(-options.Value.RetentionDays);

        // System job across all tenants: no tenant is set, so the tenant filter must be bypassed on purpose.
        var deleted = await db.DriverPositions
            .IgnoreQueryFilters()
            .Where(p => p.RecordedAt < cutoff)
            .ExecuteDeleteAsync(ct).ConfigureAwait(false);
        if (deleted > 0)
        {
            LogDeleted(logger, deleted);
        }

        return deleted;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} expired driver positions")]
    private static partial void LogDeleted(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Driver position retention failed")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
