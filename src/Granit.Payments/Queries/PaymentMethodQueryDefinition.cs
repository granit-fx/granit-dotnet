using Granit.Payments.Domain;
using Granit.QueryEngine;

namespace Granit.Payments.Queries;

/// <summary>
/// Query definition for payment methods — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class PaymentMethodQueryDefinition : QueryDefinition<PaymentMethod>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Payments.PaymentMethodQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<PaymentMethod> builder)
    {
        builder
            .Column(m => m.TenantId, c => c.Label("Tenant").LabelKey("Payments.Columns.Tenant").Filterable().Sortable())
            .Column(m => m.Type, c => c.Label("Type").LabelKey("Payments.Columns.MethodType").Filterable().Sortable())
            .Column(m => m.ProviderName, c => c.Label("Provider").LabelKey("Payments.Columns.Provider").Filterable().Sortable())
            .Column(m => m.IsDefault, c => c.Label("Default").LabelKey("Payments.Columns.IsDefault").Filterable().Sortable())
            .Column(m => m.ExpiresAt, c => c.Label("Expires At").LabelKey("Payments.Columns.ExpiresAt").Sortable())
            .Column(m => m.CreatedAt, c => c.Label("Created At").LabelKey("Payments.Columns.CreatedAt").Sortable())
            .Column(m => m.ModifiedAt, c => c.Label("Modified At").LabelKey("Payments.Columns.ModifiedAt").Sortable())
            .GlobalSearch(m => m.Type, m => m.ProviderName)
            .DateFilter(m => m.CreatedAt)
            .DefaultSort("-createdAt")
            .DefaultPageSize(25);
    }
}
