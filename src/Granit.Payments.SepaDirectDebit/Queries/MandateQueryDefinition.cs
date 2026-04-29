using Granit.Payments.SepaDirectDebit.Domain;
using Granit.QueryEngine;

namespace Granit.Payments.SepaDirectDebit.Queries;

/// <summary>
/// Query definition for SEPA Direct Debit mandates — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class MandateQueryDefinition : QueryDefinition<Mandate>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Payments.SepaDirectDebit.MandateQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<Mandate> builder)
    {
        builder
            // Identity
            .Column(m => m.TenantId, c => c.Label("Tenant").LabelKey("Payments.SepaDirectDebit.Columns.Tenant").Filterable().Sortable())
            .Column(m => m.MandateReference, c => c.Label("Mandate Reference").LabelKey("Payments.SepaDirectDebit.Columns.MandateReference").Filterable().Sortable())
            // State
            .Column(m => m.Status, c => c.Label("Status").LabelKey("Payments.SepaDirectDebit.Columns.Status").Filterable().Sortable())
            .Column(m => m.Scheme, c => c.Label("Scheme").LabelKey("Payments.SepaDirectDebit.Columns.Scheme").Filterable().Sortable())
            // Debtor (PII / banking — admin grid only, gated by *.Read permission)
            .Column(m => m.DebtorName, c => c.Label("Debtor Name").LabelKey("Payments.SepaDirectDebit.Columns.DebtorName").Filterable().Sortable())
            .Column(m => m.DebtorIban, c => c.Label("Debtor IBAN").LabelKey("Payments.SepaDirectDebit.Columns.DebtorIban").Filterable())
            .Column(m => m.DebtorBic, c => c.Label("Debtor BIC").LabelKey("Payments.SepaDirectDebit.Columns.DebtorBic").Filterable())
            // Creditor / provider
            .Column(m => m.CreditorId, c => c.Label("Creditor ID").LabelKey("Payments.SepaDirectDebit.Columns.CreditorId").Filterable())
            .Column(m => m.ProviderName, c => c.Label("Provider").LabelKey("Payments.SepaDirectDebit.Columns.Provider").Filterable().Sortable())
            .Column(m => m.ProviderMandateId, c => c.Label("Provider Mandate ID").LabelKey("Payments.SepaDirectDebit.Columns.ProviderMandateId").Filterable())
            // Lifecycle
            .Column(m => m.SignedAt, c => c.Label("Signed At").LabelKey("Payments.SepaDirectDebit.Columns.SignedAt").Sortable())
            .Column(m => m.ActivatedAt, c => c.Label("Activated At").LabelKey("Payments.SepaDirectDebit.Columns.ActivatedAt").Sortable())
            .Column(m => m.CancelledAt, c => c.Label("Cancelled At").LabelKey("Payments.SepaDirectDebit.Columns.CancelledAt").Sortable())
            .Column(m => m.LastCollectionAt, c => c.Label("Last Collection At").LabelKey("Payments.SepaDirectDebit.Columns.LastCollectionAt").Sortable())
            // Audit
            .Column(m => m.CreatedAt, c => c.Label("Created At").LabelKey("Payments.SepaDirectDebit.Columns.CreatedAt").Sortable())
            .Column(m => m.ModifiedAt, c => c.Label("Modified At").LabelKey("Payments.SepaDirectDebit.Columns.ModifiedAt").Sortable())
            .GlobalSearch(m => m.MandateReference, m => m.CreditorId, m => m.ProviderMandateId, m => m.DebtorName, m => m.DebtorIban)
            .DateFilter(m => m.SignedAt)
            .DefaultSort("-signedAt")
            .DefaultPageSize(25);
    }
}
