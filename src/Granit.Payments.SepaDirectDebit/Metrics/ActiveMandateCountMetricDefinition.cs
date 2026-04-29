using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Payments.SepaDirectDebit.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Payments.SepaDirectDebit.Metrics;

/// <summary>
/// Number of SEPA mandates in <see cref="MandateStatus.Active"/> state — those that
/// can carry collections. Headline operational KPI for direct-debit revenue: this
/// is the addressable base for the next billing run.
/// </summary>
public sealed class ActiveMandateCountMetricDefinition : MetricDefinition<Mandate, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Payments.SepaDirectDebit.ActiveMandateCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<Mandate, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<Mandate, bool>>? BaseFilter
        => m => m.Status == MandateStatus.Active;

    /// <inheritdoc />
    public override Expression<Func<Mandate, DateTimeOffset>>? PeriodSelector
        => m => m.ActivatedAt!.Value;
}
