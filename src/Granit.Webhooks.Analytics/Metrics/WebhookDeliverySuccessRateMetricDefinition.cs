using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.QueryEngine.Filtering;
using Granit.Webhooks.Domain;

namespace Granit.Webhooks.Analytics.Metrics;

/// <summary>
/// Average success ratio of webhook delivery attempts in the period — projects
/// each attempt to <c>1</c> on success / <c>0</c> on failure and averages, giving
/// a value in <c>[0, 1]</c> rendered as a percentage. EF Core translates the
/// conditional projection to <c>CASE WHEN ... THEN 1 ELSE 0 END</c>.
/// </summary>
/// <remarks>
/// Empty-set semantics: <see cref="AggregateFunction.Avg"/> over an empty set
/// returns <c>null</c> per the framework contract (story #1374) — "no data" is
/// not the same as "0% success".
/// </remarks>
public sealed class WebhookDeliverySuccessRateMetricDefinition : MetricDefinition<WebhookDeliveryAttempt, double>
{
    /// <inheritdoc />
    public override string Name => "Granit.Webhooks.WebhookDeliverySuccessRateMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Percentage;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Avg;

    /// <inheritdoc />
    public override Expression<Func<WebhookDeliveryAttempt, double?>>? Selector
        => a => a.IsSuccess ? 1.0 : 0.0;

    /// <inheritdoc />
    public override Expression<Func<WebhookDeliveryAttempt, DateTimeOffset>>? PeriodSelector
        => a => a.OccurredAt;
}
