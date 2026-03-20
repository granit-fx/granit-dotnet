namespace Granit.Diagnostics.Options;

/// <summary>
/// Configuration options for Granit health check endpoints.
/// </summary>
public sealed class DiagnosticsOptions
{
    /// <summary>Path for the liveness probe. Default: <c>/health/live</c>.</summary>
    public string LivenessPath { get; set; } = "/health/live";

    /// <summary>Path for the readiness probe. Default: <c>/health/ready</c>.</summary>
    public string ReadinessPath { get; set; } = "/health/ready";

    /// <summary>Path for the startup probe. Default: <c>/health/startup</c>.</summary>
    public string StartupPath { get; set; } = "/health/startup";

    /// <summary>
    /// Default cache duration for <see cref="Caching.CachedHealthCheck"/>.
    /// Default: 10 seconds.
    /// </summary>
    public TimeSpan DefaultCacheDuration { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Cache duration for the monitoring aggregator (<see cref="Abstractions.IHealthCheckAggregator"/>).
    /// Aligns with the typical dashboard auto-refresh interval.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan MonitoringCacheDuration { get; set; } = TimeSpan.FromSeconds(30);
}
