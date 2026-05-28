using Granit.Vault.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Vault.Services;

/// <summary>
/// Periodically refreshes the keys cached by <see cref="SecretBackedMacService"/>.
/// Cadence is set by <see cref="SecretBackedMacOptions.RefreshInterval"/>.
/// </summary>
internal sealed partial class SecretBackedMacRefreshHostedService(
    SecretBackedMacService service,
    IOptions<SecretBackedMacOptions> options,
    ILogger<SecretBackedMacRefreshHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan interval = options.Value.RefreshInterval;

        // Warm cache at startup so the first MacAsync call does not pay the round-trip.
        try
        {
            await service.RefreshAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            LogStartupRefreshFailed(logger, ex);
        }

        using PeriodicTimer timer = new(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await service.RefreshAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogPeriodicRefreshFailed(logger, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "SecretBackedMacService startup refresh failed — first MacAsync call will retry. The service is unusable until the key can be loaded.")]
    private static partial void LogStartupRefreshFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "SecretBackedMacService periodic refresh failed — keeping previously cached keys.")]
    private static partial void LogPeriodicRefreshFailed(ILogger logger, Exception ex);
}
