using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Invoicing.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Invoicing.Metrics;

/// <summary>
/// Total monetary amount of credit notes issued in the period — sum of
/// <see cref="Invoice.Total"/> across <see cref="InvoiceDocumentType.CreditNote"/>
/// documents finalised in the window. Reads as the negative-revenue
/// counterpart of <c>IssuedInvoiceTotal</c>; high values usually indicate
/// disputed invoices, partial refunds, or pricing errors. Lower is better.
/// </summary>
public sealed class CreditNoteTotalMetricDefinition : MetricDefinition<Invoice, decimal>
{
    /// <inheritdoc />
    public override string Name => "Granit.Invoicing.CreditNoteTotalMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Currency;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Sum;

    /// <inheritdoc />
    public override Expression<Func<Invoice, decimal?>>? Selector
        => i => i.Total;

    /// <inheritdoc />
    public override Expression<Func<Invoice, bool>>? BaseFilter
        => i => i.DocumentType == InvoiceDocumentType.CreditNote
             && i.Status != InvoiceStatus.Draft
             && i.IssuedAt != null;

    /// <inheritdoc />
    public override Expression<Func<Invoice, DateTimeOffset>>? PeriodSelector
        => i => i.IssuedAt!.Value;

    /// <inheritdoc />
    public override bool IsHigherBetter => false;
}
