using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Invoicing.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Invoicing.Metrics;

/// <summary>
/// Number of <see cref="InvoiceDocumentType.Invoice"/> documents finalised in
/// the period — drafts excluded. Period selector is
/// <see cref="Invoice.IssuedAt"/> so <c>?period=mtd</c> answers "how many
/// invoices did we issue this month?".
/// </summary>
public sealed class IssuedInvoiceCountMetricDefinition : MetricDefinition<Invoice, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Invoicing.IssuedInvoiceCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<Invoice, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<Invoice, bool>>? BaseFilter
        => i => i.DocumentType == InvoiceDocumentType.Invoice
             && i.Status != InvoiceStatus.Draft
             && i.IssuedAt != null;

    /// <inheritdoc />
    public override Expression<Func<Invoice, DateTimeOffset>>? PeriodSelector
        => i => i.IssuedAt!.Value;
}
