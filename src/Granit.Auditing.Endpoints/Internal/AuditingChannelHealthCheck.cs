using System.Threading.Channels;
using Granit.Auditing.Domain;
using Granit.Auditing.Messages;
using Granit.Auditing.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Auditing.Endpoints.Internal;

/// <summary>
/// Health check for the async audit persistence channel, surfacing backpressure to the
/// Kubernetes readiness probe before the bounded channel saturates and starts blocking
/// request threads (<c>BoundedChannelFullMode.Wait</c>).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Healthy</b>: fill ratio below 80% (or <see cref="AuditPersistenceMode.Strict"/>, where the channel is unused).</item>
/// <item><b>Degraded</b>: fill ratio at or above 80% — backpressure is imminent.</item>
/// <item><b>Unhealthy</b>: channel saturated — producers (interceptors) are blocked waiting for capacity.</item>
/// </list>
/// Exposes only counters/ratios — never any audited payload.
/// </remarks>
internal sealed class AuditingChannelHealthCheck(
    Channel<AuditingBatch> channel,
    IOptions<AuditingOptions> options) : IHealthCheck
{
    private const double DegradedThreshold = 0.8;

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        AuditingOptions opts = options.Value;

        // Strict mode persists synchronously and never writes to the channel — nothing to monitor.
        if (opts.PersistenceMode != AuditPersistenceMode.Async)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                "Auditing persistence mode is Strict; the async channel is unused."));
        }

        if (!channel.Reader.CanCount)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Audit channel depth is not observable."));
        }

        int depth = channel.Reader.Count;
        int capacity = opts.ChannelCapacity;
        double fillRatio = capacity > 0 ? (double)depth / capacity : 0;

        Dictionary<string, object> data = new()
        {
            ["depth"] = depth,
            ["capacity"] = capacity,
            ["fill_ratio"] = fillRatio,
        };

        if (depth >= capacity)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Audit channel saturated: {depth}/{capacity} ({fillRatio:P0}) — producers are blocked on backpressure.",
                data: data));
        }

        if (fillRatio >= DegradedThreshold)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Audit channel under pressure: {depth}/{capacity} ({fillRatio:P0}).",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Audit channel healthy: {depth}/{capacity} ({fillRatio:P0}).",
            data: data));
    }
}
