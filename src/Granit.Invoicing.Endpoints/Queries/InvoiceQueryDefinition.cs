using Granit.Invoicing.Domain;
using Granit.QueryEngine;
using Granit.QueryEngine.Filtering;

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
    protected override void Configure(QueryDefinitionBuilder<Invoice> builder)
    {
        builder
            .Column(i => i.TenantId, c => c.Label("Tenant").Filterable().Sortable())
            .Column(i => i.InvoiceNumber, c => c.Label("Invoice Number").Filterable().Sortable())
            .Column(i => i.DocumentType, c => c.Label("Document Type").Filterable().Sortable())
            .Column(i => i.Status, c => c.Label("Status").Filterable().Sortable())
            .Column(i => i.Currency, c => c.Label("Currency").Filterable().Sortable())
            .Column(i => i.Total, c => c.Label("Total").Sortable())
            .Column(i => i.AmountRemaining, c => c.Label("Amount Remaining").Sortable())
            .Column(i => i.IssuedAt, c => c.Label("Issued At").Sortable())
            .Column(i => i.DueAt, c => c.Label("Due At").Sortable())
            .Column(i => i.PaidAt, c => c.Label("Paid At").Sortable())
            .Column(i => i.BillingReason, c => c.Label("Billing Reason").Filterable())
            .GlobalSearch(i => i.InvoiceNumber, i => i.Currency)
            .DateFilter(i => i.IssuedAt)
            .DefaultSort("-issuedAt")
            .DefaultPageSize(25);
    }
}
