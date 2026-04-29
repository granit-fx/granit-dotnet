using Granit.Invoicing.Domain;
using Granit.Invoicing.Internal;
using Granit.QueryEngine;

namespace Granit.Invoicing.Queries;

/// <summary>
/// Query definition for invoices — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class InvoiceQueryDefinition : QueryDefinition<Invoice>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Invoicing.InvoiceQuery";

    /// <inheritdoc/>
    public override Type? LocalizationResourceType => typeof(InvoicingLocalizationResource);

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<Invoice> builder)
    {
        builder
            // ── Identity, party, document classification ─────────────────────
            .Column(i => i.TenantId, c => c.Label("Tenant").LabelKey("Invoicing.Columns.Tenant").Filterable().Sortable())
            .Column(i => i.InvoiceNumber, c => c.Label("Invoice Number").LabelKey("Invoicing.Columns.InvoiceNumber").Filterable().Sortable())
            .Column(i => i.PartyId, c => c.Label("Party").LabelKey("Invoicing.Columns.Party").Filterable())
            .Column(i => i.DocumentType, c => c.Label("Document Type").LabelKey("Invoicing.Columns.DocumentType").Filterable().Sortable())
            .Column(i => i.Status, c => c.Label("Status").LabelKey("Invoicing.Columns.Status").Filterable().Sortable())
            .Column(i => i.CollectionMethod, c => c.Label("Collection Method").LabelKey("Invoicing.Columns.CollectionMethod").Filterable().Sortable())
            .Column(i => i.BillingReason, c => c.Label("Billing Reason").LabelKey("Invoicing.Columns.BillingReason").Filterable())
            // ── Credit-note linkage ──────────────────────────────────────────
            .Column(i => i.ParentInvoiceId, c => c.Label("Parent Invoice").LabelKey("Invoicing.Columns.ParentInvoice").Filterable())
            .Column(i => i.CreditNoteReason, c => c.Label("Credit Note Reason").LabelKey("Invoicing.Columns.CreditNoteReason").Filterable())
            // ── Money: subtotals, totals, payments, refunds ──────────────────
            .Column(i => i.Currency, c => c.Label("Currency").LabelKey("Invoicing.Columns.Currency").Filterable().Sortable())
            .Column(i => i.Subtotal, c => c.Label("Subtotal").LabelKey("Invoicing.Columns.Subtotal").Sortable())
            .Column(i => i.TaxTotal, c => c.Label("Tax Total").LabelKey("Invoicing.Columns.TaxTotal").Sortable())
            .Column(i => i.Total, c => c.Label("Total").LabelKey("Invoicing.Columns.Total").Sortable())
            .Column(i => i.AmountPaid, c => c.Label("Amount Paid").LabelKey("Invoicing.Columns.AmountPaid").Sortable())
            .Column(i => i.AmountCredited, c => c.Label("Amount Credited").LabelKey("Invoicing.Columns.AmountCredited").Sortable())
            .Column(i => i.AmountRemaining, c => c.Label("Amount Remaining").LabelKey("Invoicing.Columns.AmountRemaining").Sortable())
            .Column(i => i.Overpayment, c => c.Label("Overpayment").LabelKey("Invoicing.Columns.Overpayment").Sortable())
            // ── Time: lifecycle dates + service period ───────────────────────
            .Column(i => i.IssuedAt, c => c.Label("Issued At").LabelKey("Invoicing.Columns.IssuedAt").Sortable())
            .Column(i => i.DueAt, c => c.Label("Due At").LabelKey("Invoicing.Columns.DueAt").Sortable())
            .Column(i => i.PaidAt, c => c.Label("Paid At").LabelKey("Invoicing.Columns.PaidAt").Sortable())
            .Column(i => i.PeriodStart, c => c.Label("Period Start").LabelKey("Invoicing.Columns.PeriodStart").Sortable())
            .Column(i => i.PeriodEnd, c => c.Label("Period End").LabelKey("Invoicing.Columns.PeriodEnd").Sortable())
            .GlobalSearch(i => i.InvoiceNumber, i => i.Currency)
            .DateFilter(i => i.IssuedAt)
            .DefaultSort("-issuedAt")
            .DefaultPageSize(25);
    }
}
