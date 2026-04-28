using Granit.Analytics.Endpoints.Dtos;
using Granit.Analytics.Metrics;

namespace Granit.Analytics.Endpoints.Internal;

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
    Task<decimal?> ExecuteAsync(ResolvedPeriod? period, CancellationToken cancellationToken);

    /// <summary>The metric's value kind (count, currency, percentage, ...).</summary>
    MetricValueKind ValueKind { get; }

    /// <summary>ISO 4217 currency code for currency-kind metrics, otherwise null.</summary>
    string? CurrencyCode { get; }

    /// <summary>Whether higher values are favorable.</summary>
    bool IsHigherBetter { get; }
}
