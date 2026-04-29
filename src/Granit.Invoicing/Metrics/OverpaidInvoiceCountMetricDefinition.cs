using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Invoicing.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Invoicing.Metrics;

/// <summary>
/// Number of invoices carrying a non-zero <see cref="Invoice.Overpayment"/>
/// in the period — invoices where the customer paid more than the billed
/// amount. Each overpayment requires reconciliation (refund, customer
/// credit, or write-off); high counts indicate friction in the
/// pay-and-reconcile flow. Lower is better.
/// </summary>
public sealed class OverpaidInvoiceCountMetricDefinition : MetricDefinition<Invoice, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Invoicing.OverpaidInvoiceCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<Invoice, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<Invoice, bool>>? BaseFilter
        => i => i.Overpayment > 0m && i.PaidAt != null;

    /// <inheritdoc />
    public override Expression<Func<Invoice, DateTimeOffset>>? PeriodSelector
        => i => i.PaidAt!.Value;

    /// <inheritdoc />
    public override bool IsHigherBetter => false;
}
