using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Granit.Http.OutputCaching.StackExchangeRedis.HealthChecks;

/// <summary>
/// Health check that verifies Redis connectivity for the output cache store
/// by issuing a PING command and measuring round-trip latency.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Latency &lt; threshold → <see cref="HealthCheckResult.Healthy"/></item>
///   <item>Latency ≥ threshold → <see cref="HealthCheckResult.Degraded"/></item>
///   <item>Unreachable → <see cref="HealthCheckResult.Unhealthy"/></item>
/// </list>
/// The response never exposes connection strings or authentication credentials.
/// </remarks>
internal sealed class RedisOutputCacheHealthCheck(
    IConnectionMultiplexer connection,
    TimeSpan degradedThreshold) : IHealthCheck
{
    private static readonly TimeSpan s_healthCheckTimeout = TimeSpan.FromSeconds(10);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            TimeSpan latency = await connection.GetDatabase().PingAsync()
                .WaitAsync(s_healthCheckTimeout, cancellationToken).ConfigureAwait(false);

            Dictionary<string, object> data = new()
            {
                ["latency_ms"] = latency.TotalMilliseconds,
                ["threshold_ms"] = degradedThreshold.TotalMilliseconds,
            };

            if (latency >= degradedThreshold)
            {
                return HealthCheckResult.Degraded(
                    $"Redis output cache latency: {latency.TotalMilliseconds:0} ms (threshold: {degradedThreshold.TotalMilliseconds:0} ms)",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                $"Redis output cache latency: {latency.TotalMilliseconds:0} ms",
                data: data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Redis output cache unreachable: {ex.GetType().Name}");
        }
    }
}
