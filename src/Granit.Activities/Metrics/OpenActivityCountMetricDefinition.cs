using System.Linq.Expressions;
using Granit.Activities.Domain;
using Granit.Analytics.Metrics;
using Granit.QueryEngine.Filtering;

namespace Granit.Activities.Metrics;

/// <summary>
/// Number of activities currently in <see cref="ActivityStatus.Open"/> — the
/// outstanding-work KPI for the activities admin dashboard. Done and Cancelled
/// activities are excluded by design: they would inflate the KPI with
/// already-closed work.
/// </summary>
/// <remarks>
/// <para>
/// Period selector is <see cref="Activity.DueAt"/>. With <c>?period=last_7d</c>
/// the count is restricted to open activities due in that window
/// ("how many open items are due in the next 7 days?" when paired with a
/// forward-looking token, "how many open items were due in the last 7 days?"
/// otherwise). Without a period parameter, returns the all-time open count.
/// </para>
/// <para>
/// <c>IsHigherBetter</c> is <c>false</c> — fewer open activities is favourable,
/// so the delta arrow renders red when the value goes up.
/// </para>
/// </remarks>
public sealed class OpenActivityCountMetricDefinition : MetricDefinition<Activity, int>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Activities.OpenActivityCountMetric";

    /// <inheritdoc/>
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc/>
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc/>
    public override Expression<Func<Activity, int?>>? Selector => null;

    /// <inheritdoc/>
    public override Expression<Func<Activity, bool>>? BaseFilter
        => a => a.Status == ActivityStatus.Open;

    /// <inheritdoc/>
    public override Expression<Func<Activity, DateTimeOffset>>? PeriodSelector
        => a => a.DueAt;

    /// <inheritdoc/>
    public override bool IsHigherBetter => false;
}
