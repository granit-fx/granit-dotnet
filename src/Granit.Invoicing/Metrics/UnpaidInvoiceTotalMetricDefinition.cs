using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Invoicing.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Invoicing.Metrics;

/// <summary>
/// Total monetary amount currently outstanding on <see cref="InvoiceStatus.Open"/>
/// invoices — sum of <see cref="Invoice.AmountRemaining"/> per invoice, so a partially
/// paid invoice contributes only the unpaid balance, not its full <c>Total</c>.
/// </summary>
/// <remarks>
/// <para>
/// Currency is intentionally not declared here — different invoices may carry
/// different ISO 4217 codes per <see cref="Invoice.Currency"/>. The metric value is
/// the raw sum; the host application is responsible for currency normalisation when
/// displaying mixed-currency tenants. Single-currency tenants render the value with
/// their tenant's configured currency in the frontend.
/// </para>
/// <para>
/// Period selector is <see cref="Invoice.IssuedAt"/>; <c>IsHigherBetter</c> is
/// <c>false</c> — less unpaid debt is favourable.
/// </para>
/// </remarks>
public sealed class UnpaidInvoiceTotalMetricDefinition : MetricDefinition<Invoice, decimal>
{
    /// <inheritdoc />
    public override string Name => "Granit.Invoicing.UnpaidInvoiceTotalMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Currency;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Sum;

    /// <inheritdoc />
    public override Expression<Func<Invoice, decimal?>>? Selector => i => i.AmountRemaining;

    /// <inheritdoc />
    public override Expression<Func<Invoice, bool>>? BaseFilter
        => i => i.Status == InvoiceStatus.Open;

    /// <inheritdoc />
    public override Expression<Func<Invoice, DateTimeOffset>>? PeriodSelector
        => i => i.IssuedAt!.Value;

    /// <inheritdoc />
    public override bool IsHigherBetter => false;
}
