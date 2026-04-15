using Granit.Invoicing.Domain;
using Granit.Invoicing.Endpoints.Internal;
using Granit.QueryEngine;

namespace Granit.Invoicing.Endpoints.Queries;

/// <summary>
/// Query definition for invoices — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class InvoiceQueryDefinition : QueryDefinition<Invoice>
{
    /// <inheritdoc/>
    public override string Name => "Invoicing.Invoices";

    /// <inheritdoc/>
    public override Type? LocalizationResourceType => typeof(InvoicingEndpointsLocalizationResource);

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<Invoice> builder)
    {
        builder
            .Column(i => i.TenantId, c => c.Label("Tenant").LabelKey("Invoicing.Columns.Tenant").Filterable().Sortable())
            .Column(i => i.InvoiceNumber, c => c.Label("Invoice Number").LabelKey("Invoicing.Columns.InvoiceNumber").Filterable().Sortable())
            .Column(i => i.DocumentType, c => c.Label("Document Type").LabelKey("Invoicing.Columns.DocumentType").Filterable().Sortable())
            .Column(i => i.Status, c => c.Label("Status").LabelKey("Invoicing.Columns.Status").Filterable().Sortable())
            .Column(i => i.Currency, c => c.Label("Currency").LabelKey("Invoicing.Columns.Currency").Filterable().Sortable())
            .Column(i => i.Total, c => c.Label("Total").LabelKey("Invoicing.Columns.Total").Sortable())
            .Column(i => i.AmountRemaining, c => c.Label("Amount Remaining").LabelKey("Invoicing.Columns.AmountRemaining").Sortable())
            .Column(i => i.IssuedAt, c => c.Label("Issued At").LabelKey("Invoicing.Columns.IssuedAt").Sortable())
            .Column(i => i.DueAt, c => c.Label("Due At").LabelKey("Invoicing.Columns.DueAt").Sortable())
            .Column(i => i.PaidAt, c => c.Label("Paid At").LabelKey("Invoicing.Columns.PaidAt").Sortable())
            .Column(i => i.BillingReason, c => c.Label("Billing Reason").LabelKey("Invoicing.Columns.BillingReason").Filterable())
            .GlobalSearch(i => i.InvoiceNumber, i => i.Currency)
            .DateFilter(i => i.IssuedAt)
            .DefaultSort("-issuedAt")
            .DefaultPageSize(25);
    }
}
