using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Payments.SepaDirectDebit.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Payments.SepaDirectDebit.Metrics;

/// <summary>
/// Number of SEPA mandates revoked in the period (period selector is
/// <see cref="Mandate.CancelledAt"/>). Tracks mandate churn — the SEPA-specific
/// counterpart to subscription cancellations.
/// </summary>
public sealed class CancelledMandateCountMetricDefinition : MetricDefinition<Mandate, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Payments.SepaDirectDebit.CancelledMandateCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<Mandate, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<Mandate, bool>>? BaseFilter
        => m => m.Status == MandateStatus.Cancelled;

    /// <inheritdoc />
    public override Expression<Func<Mandate, DateTimeOffset>>? PeriodSelector
        => m => m.CancelledAt!.Value;

    /// <inheritdoc />
    public override bool IsHigherBetter => false;
}
