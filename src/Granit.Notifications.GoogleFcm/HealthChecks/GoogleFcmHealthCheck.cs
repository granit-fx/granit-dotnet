using Granit.Notifications.GoogleFcm.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.GoogleFcm.HealthChecks;

/// <summary>
/// Health check that verifies the FCM configuration is present and coherent.
/// Config-only probe — no network call, no message sent (mirrors the AwsSns /
/// AzureNotificationHubs config probes; a real FCM send would cost quota and
/// deliver to a device).
/// </summary>
internal sealed class GoogleFcmHealthCheck(
    IOptions<GoogleFcmOptions> options) : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            GoogleFcmOptions opts = options.Value;

            if (string.IsNullOrWhiteSpace(opts.ProjectId))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("FCM ProjectId is not configured."));
            }

            if (string.IsNullOrWhiteSpace(opts.ServiceAccountJson))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("FCM ServiceAccountJson is not configured."));
            }

            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception ex)
        {
            // Sanitize: never expose the service-account key material.
            return Task.FromResult(HealthCheckResult.Unhealthy($"FCM health check failed: {ex.GetType().Name}"));
        }
    }
}
