using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Invoicing.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Invoicing.Metrics;

/// <summary>
/// Total amount actually collected in the period — sum of
/// <see cref="Invoice.AmountPaid"/> across invoices that reached
/// <see cref="InvoiceStatus.Paid"/> in the window. Reads as the realised
/// cash side of <c>IssuedInvoiceTotal</c>; the gap between issued and paid
/// is the receivables ageing.
/// </summary>
public sealed class PaidInvoiceTotalMetricDefinition : MetricDefinition<Invoice, decimal>
{
    /// <inheritdoc />
    public override string Name => "Granit.Invoicing.PaidInvoiceTotalMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Currency;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Sum;

    /// <inheritdoc />
    public override Expression<Func<Invoice, decimal?>>? Selector
        => i => i.AmountPaid;

    /// <inheritdoc />
    public override Expression<Func<Invoice, bool>>? BaseFilter
        => i => i.Status == InvoiceStatus.Paid && i.PaidAt != null;

    /// <inheritdoc />
    public override Expression<Func<Invoice, DateTimeOffset>>? PeriodSelector
        => i => i.PaidAt!.Value;
}
