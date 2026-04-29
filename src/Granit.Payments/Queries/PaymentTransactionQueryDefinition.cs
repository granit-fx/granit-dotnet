using Granit.Payments.Domain;
using Granit.QueryEngine;

namespace Granit.Payments.Queries;

/// <summary>
/// Query definition for payment transactions — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class PaymentTransactionQueryDefinition : QueryDefinition<PaymentTransaction>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Payments.PaymentTransactionQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<PaymentTransaction> builder)
    {
        builder
            // Identity
            .Column(t => t.TenantId, c => c.Label("Tenant").LabelKey("Payments.Columns.Tenant").Filterable().Sortable())
            .Column(t => t.InvoiceId, c => c.Label("Invoice").LabelKey("Payments.Columns.Invoice").Filterable().Sortable())
            .Column(t => t.PaymentMethodId, c => c.Label("Payment Method").LabelKey("Payments.Columns.PaymentMethod").Filterable())
            // State
            .Column(t => t.Status, c => c.Label("Status").LabelKey("Payments.Columns.Status").Filterable().Sortable())
            // Money
            .Column(t => t.Amount, c => c.Label("Amount").LabelKey("Payments.Columns.Amount").Sortable())
            .Column(t => t.Currency, c => c.Label("Currency").LabelKey("Payments.Columns.Currency").Filterable().Sortable())
            // Provider
            .Column(t => t.ProviderName, c => c.Label("Provider").LabelKey("Payments.Columns.Provider").Filterable().Sortable())
            .Column(t => t.MethodType, c => c.Label("Method Type").LabelKey("Payments.Columns.MethodType").Filterable().Sortable())
            .Column(t => t.ProviderTransactionId, c => c.Label("Provider Tx ID").LabelKey("Payments.Columns.ProviderTransactionId").Filterable())
            .Column(t => t.IdempotencyKey, c => c.Label("Idempotency Key").LabelKey("Payments.Columns.IdempotencyKey").Filterable())
            // Failure
            .Column(t => t.FailureCode, c => c.Label("Failure Code").LabelKey("Payments.Columns.FailureCode").Filterable().Sortable())
            .Column(t => t.FailureMessage, c => c.Label("Failure Message").LabelKey("Payments.Columns.FailureMessage").Filterable())
            // Lifecycle
            .Column(t => t.CreatedAt, c => c.Label("Created At").LabelKey("Payments.Columns.CreatedAt").Sortable())
            .Column(t => t.ModifiedAt, c => c.Label("Modified At").LabelKey("Payments.Columns.ModifiedAt").Sortable())
            .Column(t => t.SucceededAt, c => c.Label("Succeeded At").LabelKey("Payments.Columns.SucceededAt").Sortable())
            .Column(t => t.CanceledAt, c => c.Label("Canceled At").LabelKey("Payments.Columns.CanceledAt").Sortable())
            .GlobalSearch(t => t.ProviderTransactionId, t => t.IdempotencyKey, t => t.FailureCode, t => t.Currency)
            .DateFilter(t => t.CreatedAt)
            .DefaultSort("-createdAt")
            .DefaultPageSize(25);
    }
}
