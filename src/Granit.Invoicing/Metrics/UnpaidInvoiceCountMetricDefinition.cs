using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Invoicing.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Invoicing.Metrics;

/// <summary>
/// Number of invoices currently in <see cref="InvoiceStatus.Open"/> status — finalised
/// invoices awaiting payment. <c>Void</c> (cancelled) and <c>Uncollectible</c>
/// (write-off) are excluded by design: they would inflate the KPI with amounts that
/// will never be collected.
/// </summary>
/// <remarks>
/// <para>
/// Period selector is <see cref="Invoice.IssuedAt"/>: when the endpoint is called with
/// <c>?period=last_30d</c>, the count is restricted to invoices issued during that
/// window. Without a period parameter, the metric returns the all-time count.
/// </para>
/// <para>
/// <c>IsHigherBetter</c> is <c>false</c> — fewer unpaid invoices is favourable for the
/// business, so the delta arrow renders red when the value goes up.
/// </para>
/// </remarks>
public sealed class UnpaidInvoiceCountMetricDefinition : MetricDefinition<Invoice, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Invoicing.UnpaidInvoiceCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<Invoice, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<Invoice, bool>>? BaseFilter
        => i => i.Status == InvoiceStatus.Open;

    /// <inheritdoc />
    public override Expression<Func<Invoice, DateTimeOffset>>? PeriodSelector
        => i => i.IssuedAt!.Value;

    /// <inheritdoc />
    public override bool IsHigherBetter => false;
}
