using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.QueryEngine.Filtering;
using Granit.Webhooks.Domain;

namespace Granit.Webhooks.Analytics.Metrics;

/// <summary>
/// Number of webhook subscriptions in <see cref="WebhookSubscriptionStatus.Active"/>
/// — the live integration surface receiving event deliveries. Suspended and
/// permanently deactivated subscriptions are excluded.
/// </summary>
public sealed class ActiveWebhookSubscriptionCountMetricDefinition : MetricDefinition<WebhookSubscription, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Webhooks.ActiveWebhookSubscriptionCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<WebhookSubscription, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<WebhookSubscription, bool>>? BaseFilter
        => s => s.Status == WebhookSubscriptionStatus.Active;

    /// <inheritdoc />
    public override Expression<Func<WebhookSubscription, DateTimeOffset>>? PeriodSelector
        => s => s.CreatedAt;
}
