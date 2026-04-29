using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Invoicing.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Invoicing.Metrics;

/// <summary>
/// Total billed amount in the period — sum of <see cref="Invoice.Total"/>
/// across <see cref="InvoiceDocumentType.Invoice"/> documents finalised in
/// the period (drafts excluded). Headline revenue indicator for the
/// invoicing module.
/// </summary>
public sealed class IssuedInvoiceTotalMetricDefinition : MetricDefinition<Invoice, decimal>
{
    /// <inheritdoc />
    public override string Name => "Granit.Invoicing.IssuedInvoiceTotalMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Currency;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Sum;

    /// <inheritdoc />
    public override Expression<Func<Invoice, decimal?>>? Selector
        => i => i.Total;

    /// <inheritdoc />
    public override Expression<Func<Invoice, bool>>? BaseFilter
        => i => i.DocumentType == InvoiceDocumentType.Invoice
             && i.Status != InvoiceStatus.Draft
             && i.IssuedAt != null;

    /// <inheritdoc />
    public override Expression<Func<Invoice, DateTimeOffset>>? PeriodSelector
        => i => i.IssuedAt!.Value;
}
