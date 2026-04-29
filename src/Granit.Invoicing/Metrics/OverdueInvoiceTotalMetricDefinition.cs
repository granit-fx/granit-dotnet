using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Invoicing.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Invoicing.Metrics;

/// <summary>
/// Total monetary amount overdue in the period — sum of
/// <see cref="Invoice.AmountRemaining"/> across <see cref="InvoiceStatus.Open"/>
/// invoices whose <c>DueAt</c> falls in the period window. The cash-exposure
/// counterpart of <c>OverdueInvoiceCount</c>; combine the two to compute
/// average overdue amount client-side. Lower is better.
/// </summary>
public sealed class OverdueInvoiceTotalMetricDefinition : MetricDefinition<Invoice, decimal>
{
    /// <inheritdoc />
    public override string Name => "Granit.Invoicing.OverdueInvoiceTotalMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Currency;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Sum;

    /// <inheritdoc />
    public override Expression<Func<Invoice, decimal?>>? Selector
        => i => i.AmountRemaining;

    /// <inheritdoc />
    public override Expression<Func<Invoice, bool>>? BaseFilter
        => i => i.Status == InvoiceStatus.Open && i.DueAt != null;

    /// <inheritdoc />
    public override Expression<Func<Invoice, DateTimeOffset>>? PeriodSelector
        => i => i.DueAt!.Value;

    /// <inheritdoc />
    public override bool IsHigherBetter => false;
}
