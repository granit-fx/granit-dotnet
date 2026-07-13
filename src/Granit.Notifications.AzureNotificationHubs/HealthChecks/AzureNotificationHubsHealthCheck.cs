using Granit.Notifications.AzureNotificationHubs.Options;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.AzureNotificationHubs.HealthChecks;

/// <summary>
/// Health check that verifies Azure Notification Hubs connectivity by retrieving
/// the hub description.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Hub reachable → <see cref="HealthCheckResult.Healthy"/></item>
///   <item>Unreachable or credentials invalid → <see cref="HealthCheckResult.Unhealthy"/></item>
/// </list>
/// The response never exposes connection strings, hub names, or other configuration details.
/// </remarks>
internal sealed class AzureNotificationHubsHealthCheck(
    IOptions<AzureNotificationHubsOptions> options) : IHealthCheck
{
    private static readonly TimeSpan s_healthCheckTimeout = TimeSpan.FromSeconds(10);

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            AzureNotificationHubsOptions opts = options.Value;

            NotificationHubClient client = new(opts.ConnectionString, opts.HubName);

            await client.GetNotificationHubJobAsync("health-check-probe", cancellationToken)
                .WaitAsync(s_healthCheckTimeout, cancellationToken)
                .ConfigureAwait(false);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            // Sanitize: never expose connection string, hub name, or other details
            return HealthCheckResult.Unhealthy($"Azure Notification Hubs unreachable: {ex.GetType().Name}");
        }
    }
}
