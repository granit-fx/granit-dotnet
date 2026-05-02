using System.Linq.Expressions;
using Granit.Activities.Domain;
using Granit.Analytics.Metrics;
using Granit.QueryEngine.Filtering;

namespace Granit.Activities.Metrics;

/// <summary>
/// Number of activities still in <see cref="ActivityStatus.Open"/> that the
/// overdue background job (story A8) has already flagged as past due — i.e.
/// rows whose <see cref="Activity.OverdueNotifiedAt"/> stamp is set. This is
/// the "currently-known overdue" count that pairs with the assignee
/// notifications shipped in A7.
/// </summary>
/// <remarks>
/// <para>
/// The overdue determination relies on the BG-job-stamped column rather than
/// an in-expression <c>UtcNow</c> comparison (forbidden by CLAUDE.md): the
/// stamp is the framework's atomic, server-controlled signal that the row has
/// crossed the deadline.
/// </para>
/// <para>
/// Period selector is <see cref="Activity.DueAt"/>; <c>?period=mtd</c> answers
/// "overdue items due so far this month". Without a period parameter, returns
/// the all-time backlog of currently-flagged overdue items.
/// </para>
/// <para>
/// <c>IsHigherBetter</c> is <c>false</c> — fewer overdue activities is
/// favourable.
/// </para>
/// </remarks>
public sealed class OverdueActivityCountMetricDefinition : MetricDefinition<Activity, int>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Activities.OverdueActivityCountMetric";

    /// <inheritdoc/>
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc/>
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc/>
    public override Expression<Func<Activity, int?>>? Selector => null;

    /// <inheritdoc/>
    public override Expression<Func<Activity, bool>>? BaseFilter
        => a => a.Status == ActivityStatus.Open && a.OverdueNotifiedAt != null;

    /// <inheritdoc/>
    public override Expression<Func<Activity, DateTimeOffset>>? PeriodSelector
        => a => a.DueAt;

    /// <inheritdoc/>
    public override bool IsHigherBetter => false;
}
