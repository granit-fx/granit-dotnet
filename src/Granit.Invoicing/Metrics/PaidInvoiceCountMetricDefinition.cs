using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Invoicing.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Invoicing.Metrics;

/// <summary>
/// Number of invoices that reached <see cref="InvoiceStatus.Paid"/> in the
/// period. Period selector is <see cref="Invoice.PaidAt"/> so the result
/// answers "how many got paid this window", not "how many were issued".
/// </summary>
public sealed class PaidInvoiceCountMetricDefinition : MetricDefinition<Invoice, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Invoicing.PaidInvoiceCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<Invoice, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<Invoice, bool>>? BaseFilter
        => i => i.Status == InvoiceStatus.Paid && i.PaidAt != null;

    /// <inheritdoc />
    public override Expression<Func<Invoice, DateTimeOffset>>? PeriodSelector
        => i => i.PaidAt!.Value;
}
