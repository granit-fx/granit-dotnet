using Granit.Identity.Federated.GoogleCloud.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Federated.GoogleCloud.HealthChecks;

/// <summary>
/// Health check that verifies Google Cloud Identity Platform configuration.
/// </summary>
internal sealed class GoogleCloudIdentityHealthCheck(
    IOptions<GoogleCloudIdentityOptions> options) : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            GoogleCloudIdentityOptions opts = options.Value;

            if (string.IsNullOrWhiteSpace(opts.ProjectId))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Firebase Auth ProjectId is not configured."));
            }

            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"Firebase Auth health check failed: {ex.GetType().Name}"));
        }
    }
}
