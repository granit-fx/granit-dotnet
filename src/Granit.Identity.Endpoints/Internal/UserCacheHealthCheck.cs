using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Granit.Identity.Endpoints.Internal;

/// <summary>
/// Health check for the identity user cache.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Healthy</b>: all entries are fresh (within staleness threshold).</item>
/// <item><b>Degraded</b>: more than 10% of entries are stale.</item>
/// <item><b>Unhealthy</b>: more than 50% of entries are stale, or the store is inaccessible.</item>
/// </list>
/// </remarks>
internal sealed class UserCacheHealthCheck(IUserCacheStats cacheStats) : IHealthCheck
{
    // Bound the store round-trips so a hung cache store fails the probe fast instead of stalling it.
    private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(10);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            int total = await cacheStats.GetCountAsync(cancellationToken)
                .WaitAsync(HealthCheckTimeout, cancellationToken).ConfigureAwait(false);

            if (total == 0)
            {
                return HealthCheckResult.Healthy("User cache is empty.");
            }

            int stale = await cacheStats.GetStaleCountAsync(cancellationToken)
                .WaitAsync(HealthCheckTimeout, cancellationToken).ConfigureAwait(false);

            double staleRatio = (double)stale / total;

            Dictionary<string, object> data = new()
            {
                ["total"] = total,
                ["stale"] = stale,
                ["stale_ratio"] = staleRatio,
            };

            if (staleRatio > 0.5)
            {
                return HealthCheckResult.Unhealthy(
                    $"User cache critically stale: {stale}/{total} entries ({staleRatio:P0}) past threshold.",
                    data: data);
            }

            if (staleRatio > 0.1)
            {
                return HealthCheckResult.Degraded(
                    $"User cache partially stale: {stale}/{total} entries ({staleRatio:P0}) past threshold.",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                $"User cache healthy: {total} entries, {stale} stale.",
                data: data);
        }
        catch (TimeoutException)
        {
            return HealthCheckResult.Unhealthy("User cache store did not respond within the health-check timeout.");
        }
        catch (Exception ex)
        {
            // Sanitize: never expose connection details in the message
            return HealthCheckResult.Unhealthy($"User cache store is inaccessible: {ex.GetType().Name}");
        }
    }
}
