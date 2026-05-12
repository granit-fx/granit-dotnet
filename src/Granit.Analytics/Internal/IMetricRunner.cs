using Granit.Analytics.Metrics;
using Granit.Analytics.Rendering;
using Granit.Dashboards;
using Granit.Timing;

namespace Granit.Analytics.Internal;

/// <summary>
/// Non-generic façade over a typed <c>MetricDefinition&lt;TEntity, TValue&gt;</c> that
/// the HTTP endpoint can resolve by metric name without knowing the closed generic types
/// up front. One runner is registered per metric definition by
/// <c>AddGranitAnalyticsEndpoints</c> at startup.
/// </summary>
internal interface IMetricRunner
{
    /// <summary>The metric's unique name.</summary>
    string Name { get; }

    /// <summary>The metric's refresh hint — drives the FusionCache TTL.</summary>
    RefreshHint RefreshHint { get; }

    /// <summary>Whether the metric declares a <c>PeriodSelector</c> (required for comparison).</summary>
    bool SupportsPeriod { get; }

    /// <summary>
    /// Executes the metric over <paramref name="period"/> and returns the value as a
    /// nullable <see cref="decimal"/> (null when Avg/Min/Max returned no rows). The
    /// runner takes care of applying the period filter through
    /// <see cref="PeriodFilterBuilder"/> when <see cref="SupportsPeriod"/> is true.
    /// </summary>
    /// <param name="period">Resolved period window, or <see langword="null"/> for non-period metrics.</param>
    /// <param name="dashboardFilters">
    /// Dashboard-level filter spec layered on top of the metric's <c>BaseFilter</c>.
    /// Keyed by field name (or <c>field.operator</c>) — see <see cref="DashboardFilterTranslator"/>.
    /// <see langword="null"/> for the inline <c>POST /metrics/{name}</c> path which has no
    /// dashboard context.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<decimal?> ExecuteAsync(
        ResolvedPeriod? period,
        IReadOnlyDictionary<string, string>? dashboardFilters,
        CancellationToken cancellationToken);

    /// <summary>The metric's value kind (count, currency, percentage, ...).</summary>
    MetricValueKind ValueKind { get; }

    /// <summary>ISO 4217 currency code for currency-kind metrics, otherwise null.</summary>
    string? CurrencyCode { get; }

    /// <summary>Whether higher values are favorable.</summary>
    bool IsHigherBetter { get; }
}
