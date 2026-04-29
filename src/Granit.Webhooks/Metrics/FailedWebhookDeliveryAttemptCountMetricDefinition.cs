using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.QueryEngine.Filtering;
using Granit.Webhooks.Domain;

namespace Granit.Webhooks.Metrics;

/// <summary>
/// Number of failed delivery attempts in the period — the actionable workload for
/// integration ops. Spikes typically indicate a downstream consumer outage worth
/// paging.
/// </summary>
public sealed class FailedWebhookDeliveryAttemptCountMetricDefinition : MetricDefinition<WebhookDeliveryAttempt, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Webhooks.FailedWebhookDeliveryAttemptCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<WebhookDeliveryAttempt, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<WebhookDeliveryAttempt, bool>>? BaseFilter
        => a => !a.IsSuccess;

    /// <inheritdoc />
    public override Expression<Func<WebhookDeliveryAttempt, DateTimeOffset>>? PeriodSelector
        => a => a.OccurredAt;

    /// <inheritdoc />
    public override bool IsHigherBetter => false;
}
