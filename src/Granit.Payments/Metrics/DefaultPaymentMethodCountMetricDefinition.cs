using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Payments.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Payments.Metrics;

/// <summary>
/// Number of payment methods flagged as <see cref="PaymentMethod.IsDefault"/> for
/// their party — proxy for "parties with a configured default payment method", a
/// useful KPI for collection automation coverage. PaymentMethod has no intrinsic
/// active/inactive flag (only <see cref="PaymentMethod.ExpiresAt"/>), so a
/// time-of-day "active count" is intentionally not exposed here — it would require
/// referencing <c>UtcNow</c> in the expression, which CLAUDE.md forbids
/// (TimeProvider / IClock convention).
/// </summary>
public sealed class DefaultPaymentMethodCountMetricDefinition : MetricDefinition<PaymentMethod, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Payments.DefaultPaymentMethodCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<PaymentMethod, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<PaymentMethod, bool>>? BaseFilter
        => m => m.IsDefault;

    /// <inheritdoc />
    public override Expression<Func<PaymentMethod, DateTimeOffset>>? PeriodSelector
        => m => m.CreatedAt;
}
