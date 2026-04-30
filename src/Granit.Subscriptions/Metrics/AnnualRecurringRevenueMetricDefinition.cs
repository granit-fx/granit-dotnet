using Granit.Analytics.Metrics;
using Granit.QueryEngine.Filtering;
using Granit.Subscriptions.Domain;

namespace Granit.Subscriptions.Metrics;

/// <summary>
/// Annual Recurring Revenue (ARR) — the canonical SaaS measure. Sum of every
/// active subscription's <i>annual-equivalent</i> contribution, derived from
/// its bound <see cref="PlanPrice"/>:
/// <list type="bullet">
///   <item><see cref="BillingInterval.Monthly"/>: <c>Amount × 12</c>.</item>
///   <item><see cref="BillingInterval.Quarterly"/>: <c>Amount × 4</c>.</item>
///   <item><see cref="BillingInterval.Yearly"/>: <c>Amount</c>.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// ARR is the annualised projection of MRR — typically <c>MRR × 12</c> at the
/// portfolio level. Computing it directly from <see cref="PlanPrice.Interval"/>
/// (rather than as <c>MRR × 12</c>) keeps it numerically stable on tenants
/// with mixed billing cycles, and matches the spec investors / boards expect.
/// </para>
/// </remarks>
public sealed class AnnualRecurringRevenueMetricDefinition
    : JoinedMetricDefinition<Subscription, PlanPrice, decimal>
{
    /// <inheritdoc />
    public override string Name => "Granit.Subscriptions.AnnualRecurringRevenueMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Currency;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Sum;

    /// <inheritdoc />
    public override System.Linq.Expressions.Expression<Func<Subscription, bool>>? BaseFilter
        => s => s.Status == SubscriptionStatus.Active;

    /// <inheritdoc />
    public override System.Linq.Expressions.Expression<Func<Subscription, DateTimeOffset>>? PeriodSelector
        => s => s.CreatedAt;

    /// <inheritdoc />
    public override IQueryable<decimal?> Project(
        IQueryable<Subscription> filteredSource,
        IQueryable<PlanPrice> joinedSource) =>
            from s in filteredSource
            join p in joinedSource on s.PlanPriceId equals p.Id
            select (decimal?)(
                p.Interval == BillingInterval.Monthly ? p.Amount * 12m
              : p.Interval == BillingInterval.Quarterly ? p.Amount * 4m
              : p.Amount);
}
