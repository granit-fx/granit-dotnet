using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Invoicing.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Invoicing.Metrics;

/// <summary>
/// Number of invoices in <see cref="InvoiceStatus.Open"/> whose <c>DueAt</c>
/// falls in the period — answers "how many open invoices are overdue?" when
/// the caller asks for a backward-looking window
/// (<c>?period=last_30d</c> ⇒ "overdue in the last 30 days"). Lower is better.
/// </summary>
/// <remarks>
/// The "now" comparison that defines overdue strictly cannot live in the
/// expression tree (<c>UtcNow</c> is forbidden by CLAUDE.md). Instead we
/// surface a period-bounded variant: callers pin the "as-of-now" by passing
/// a window ending at the current instant. The frontend's period-token
/// resolver does this transparently.
/// </remarks>
public sealed class OverdueInvoiceCountMetricDefinition : MetricDefinition<Invoice, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Invoicing.OverdueInvoiceCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<Invoice, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<Invoice, bool>>? BaseFilter
        => i => i.Status == InvoiceStatus.Open && i.DueAt != null;

    /// <inheritdoc />
    public override Expression<Func<Invoice, DateTimeOffset>>? PeriodSelector
        => i => i.DueAt!.Value;

    /// <inheritdoc />
    public override bool IsHigherBetter => false;
}
