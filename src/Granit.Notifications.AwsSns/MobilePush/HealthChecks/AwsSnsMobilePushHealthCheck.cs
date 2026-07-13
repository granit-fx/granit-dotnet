using Granit.Notifications.AwsSns.MobilePush.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.AwsSns.MobilePush.HealthChecks;

/// <summary>
/// Health check that verifies SNS mobile push configuration is valid.
/// </summary>
internal sealed class AwsSnsMobilePushHealthCheck(
    IOptions<AwsSnsMobilePushOptions> options) : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            AwsSnsMobilePushOptions opts = options.Value;

            if (string.IsNullOrWhiteSpace(opts.Region))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("SNS mobile push region is not configured."));
            }

            if (string.IsNullOrWhiteSpace(opts.PlatformApplicationArn))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("SNS PlatformApplicationArn is not configured."));
            }

            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"SNS mobile push health check failed: {ex.GetType().Name}"));
        }
    }
}
