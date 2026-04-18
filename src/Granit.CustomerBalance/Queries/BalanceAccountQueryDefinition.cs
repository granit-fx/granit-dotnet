using Granit.CustomerBalance.Domain;
using Granit.QueryEngine;

namespace Granit.CustomerBalance.Queries;

/// <summary>
/// Query definition for balance accounts — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class BalanceAccountQueryDefinition : QueryDefinition<BalanceAccount>
{
    /// <inheritdoc/>
    public override string Name => "Granit.CustomerBalance.BalanceAccountQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<BalanceAccount> builder)
    {
        builder
            .Column(a => a.TenantId, c => c.Label("Tenant").LabelKey("CustomerBalance.Columns.Tenant").Filterable().Sortable())
            .Column(a => a.Currency, c => c.Label("Currency").LabelKey("CustomerBalance.Columns.Currency").Filterable().Sortable())
            .Column(a => a.Balance, c => c.Label("Balance").LabelKey("CustomerBalance.Columns.Balance").Sortable())
            .Column(a => a.CreatedAt, c => c.Label("Created At").LabelKey("CustomerBalance.Columns.CreatedAt").Sortable())
            .Column(a => a.ModifiedAt, c => c.Label("Modified At").LabelKey("CustomerBalance.Columns.ModifiedAt").Sortable())
            .GlobalSearch(a => a.Currency)
            .DateFilter(a => a.CreatedAt)
            .DefaultSort("-createdAt")
            .DefaultPageSize(25);
    }
}
