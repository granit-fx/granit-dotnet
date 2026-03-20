using Granit.Diagnostics.Dtos;

namespace Granit.Diagnostics.Abstractions;

/// <summary>
/// Aggregates all registered <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck"/>
/// instances into a single <see cref="MonitoringHealthResponse"/> suitable for an admin monitoring dashboard.
/// </summary>
public interface IHealthCheckAggregator
{
    /// <summary>
    /// Executes all registered health checks and returns an aggregated status.
    /// Results may be cached for <see cref="Options.DiagnosticsOptions.MonitoringCacheDuration"/>.
    /// </summary>
    Task<MonitoringHealthResponse> CheckAllAsync(CancellationToken cancellationToken = default);
}
