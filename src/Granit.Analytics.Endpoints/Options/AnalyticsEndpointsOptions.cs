using System.ComponentModel.DataAnnotations;

namespace Granit.Analytics.Endpoints.Options;

/// <summary>
/// Configuration options for the Granit.Analytics endpoint surface. Bound from the
/// <c>AnalyticsEndpoints</c> section of <c>appsettings.json</c> by
/// <c>AddGranitAnalyticsEndpoints</c>; data-annotation constraints are validated on
/// startup via <c>ValidateDataAnnotations</c> + <c>ValidateOnStart</c>.
/// </summary>
public sealed class AnalyticsEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "AnalyticsEndpoints";

    /// <summary>
    /// Route prefix for analytics endpoints. Default: <c>"analytics"</c>
    /// → final route <c>/analytics/metrics/{name}</c>.
    /// </summary>
    [Required]
    [MinLength(1)]
    public string RoutePrefix { get; set; } = "analytics";

    /// <summary>
    /// OpenAPI tag for metric endpoints. Default: <c>"Analytics - Metrics"</c>
    /// (per CLAUDE.md sub-tag convention <c>&lt;Module&gt; - &lt;SubGroup&gt;</c>).
    /// </summary>
    [Required]
    [MinLength(1)]
    public string MetricsTagName { get; set; } = "Analytics - Metrics";

    /// <summary>
    /// FusionCache TTL applied to <see cref="Metrics.RefreshHint.Dynamic"/> metrics.
    /// Default: 60 seconds. Static metrics use <see cref="StaticTtl"/>; realtime metrics
    /// bypass the cache entirely — they are routed through the framework push transport
    /// (<c>Granit.Dashboards.Push</c>, ADR-043) when the host has loaded it, and
    /// degrade to <see cref="DynamicTtl"/> when it isn't loaded.
    /// </summary>
    public TimeSpan DynamicTtl { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// FusionCache TTL applied to <see cref="Metrics.RefreshHint.Static"/> metrics.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan StaticTtl { get; set; } = TimeSpan.FromMinutes(5);
}
