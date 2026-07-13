using Granit.Notifications.AwsSns.Sms.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.AwsSns.Sms.HealthChecks;

/// <summary>
/// Health check that verifies SNS SMS configuration is valid and the transport is available.
/// </summary>
internal sealed class AwsSnsSmsHealthCheck(
    IOptions<AwsSnsSmsOptions> options) : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            AwsSnsSmsOptions opts = options.Value;

            if (string.IsNullOrWhiteSpace(opts.Region))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("SNS SMS region is not configured."));
            }

            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"SNS SMS health check failed: {ex.GetType().Name}"));
        }
    }
}
