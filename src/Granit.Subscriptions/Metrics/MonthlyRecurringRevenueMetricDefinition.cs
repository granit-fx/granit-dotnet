using Granit.Analytics.Metrics;
using Granit.QueryEngine.Filtering;
using Granit.Subscriptions.Domain;

namespace Granit.Subscriptions.Metrics;

/// <summary>
/// Monthly Recurring Revenue (MRR) — the canonical SaaS measure. Sum of every
/// active subscription's <i>monthly-equivalent</i> contribution, derived from
/// its bound <see cref="PlanPrice"/>:
/// <list type="bullet">
///   <item><see cref="BillingInterval.Monthly"/>: <c>Amount</c>.</item>
///   <item><see cref="BillingInterval.Quarterly"/>: <c>Amount / 3</c>.</item>
///   <item><see cref="BillingInterval.Yearly"/>: <c>Amount / 12</c>.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// The <c>?period=</c> query parameter scopes the OBSERVATION window (when
/// the subscription was created) without changing the per-row monthly
/// normalisation — that's the canonical SaaS-MRR semantic, distinct from "sum
/// of recurring revenue billed in the period" (which would require a separate
/// metric).
/// </para>
/// <para>
/// Implemented as a <see cref="JoinedMetricDefinition{TEntity, TJoined, TValue}"/>
/// because the per-row contribution is computed from <see cref="PlanPrice.Amount"/>
/// and <see cref="PlanPrice.Interval"/> on the joined row, not from a column
/// on <see cref="Subscription"/>. Subscriptions without a bound
/// <see cref="Subscription.PlanPriceId"/> are excluded from the projection
/// (LINQ inner-join semantics).
/// </para>
/// </remarks>
public sealed class MonthlyRecurringRevenueMetricDefinition
    : JoinedMetricDefinition<Subscription, PlanPrice, decimal>
{
    /// <inheritdoc />
    public override string Name => "Granit.Subscriptions.MonthlyRecurringRevenueMetric";

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
                p.Interval == BillingInterval.Yearly ? p.Amount / 12m
              : p.Interval == BillingInterval.Quarterly ? p.Amount / 3m
              : p.Amount);
}
