using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Payments.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Payments.Metrics;

/// <summary>
/// Total monetary amount under dispute in <see cref="DisputeStatus.Open"/> state —
/// sum of <see cref="Dispute.Amount"/> across open chargebacks. The metric tracks
/// the cash exposure: every euro counted here may be reversed if the cardholder wins.
/// </summary>
public sealed class OpenDisputeTotalMetricDefinition : MetricDefinition<Dispute, decimal>
{
    /// <inheritdoc />
    public override string Name => "Granit.Payments.OpenDisputeTotalMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Currency;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Sum;

    /// <inheritdoc />
    public override Expression<Func<Dispute, decimal?>>? Selector
        => d => d.Amount;

    /// <inheritdoc />
    public override Expression<Func<Dispute, bool>>? BaseFilter
        => d => d.Status == DisputeStatus.Open;

    /// <inheritdoc />
    public override Expression<Func<Dispute, DateTimeOffset>>? PeriodSelector
        => d => d.CreatedAt;

    /// <inheritdoc />
    public override bool IsHigherBetter => false;
}
