using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.QueryEngine.Filtering;
using Granit.Webhooks.Domain;

namespace Granit.Webhooks.Analytics.Metrics;

/// <summary>
/// Average end-to-end delivery latency in milliseconds across attempts in the
/// period — sourced from the pre-computed <see cref="WebhookDeliveryAttempt.DurationMs"/>
/// (HTTP request + response wall time, not queue dwell time). Rendered as a plain
/// number; the host can format with a unit suffix in the UI.
/// </summary>
/// <remarks>
/// MetricValueKind is <see cref="MetricValueKind.Number"/> rather than
/// <see cref="MetricValueKind.Duration"/> because <c>Duration</c> in the framework
/// is conventionally seconds; exposing the raw millisecond value as Number avoids
/// a silent unit conversion (and breaking older renderers that assume seconds).
/// </remarks>
public sealed class WebhookDeliveryLatencyAverageMetricDefinition : MetricDefinition<WebhookDeliveryAttempt, double>
{
    /// <inheritdoc />
    public override string Name => "Granit.Webhooks.WebhookDeliveryLatencyAverageMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Number;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Avg;

    /// <inheritdoc />
    public override Expression<Func<WebhookDeliveryAttempt, double?>>? Selector
        => a => (double)a.DurationMs;

    /// <inheritdoc />
    public override Expression<Func<WebhookDeliveryAttempt, DateTimeOffset>>? PeriodSelector
        => a => a.OccurredAt;

    /// <inheritdoc />
    public override bool IsHigherBetter => false;
}
