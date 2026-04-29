using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Payments.SepaDirectDebit.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Payments.SepaDirectDebit.Metrics;

/// <summary>
/// Number of SEPA mandates in <see cref="MandateStatus.Pending"/> state — created
/// but not yet signed by the customer or approved by the provider. Persistent
/// values typically indicate a stuck onboarding flow worth investigating.
/// </summary>
public sealed class PendingMandateCountMetricDefinition : MetricDefinition<Mandate, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Payments.SepaDirectDebit.PendingMandateCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<Mandate, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<Mandate, bool>>? BaseFilter
        => m => m.Status == MandateStatus.Pending;

    /// <inheritdoc />
    public override Expression<Func<Mandate, DateTimeOffset>>? PeriodSelector
        => m => m.CreatedAt;
}
