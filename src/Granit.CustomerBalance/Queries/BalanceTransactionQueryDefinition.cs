using Granit.CustomerBalance.Domain;
using Granit.QueryEngine;

namespace Granit.CustomerBalance.Queries;

/// <summary>
/// Query definition for balance transactions — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class BalanceTransactionQueryDefinition : QueryDefinition<BalanceTransaction>
{
    /// <inheritdoc/>
    public override string Name => "Granit.CustomerBalance.BalanceTransactionQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<BalanceTransaction> builder)
    {
        builder
            .Column(t => t.BalanceAccountId, c => c.Label("Balance Account").LabelKey("CustomerBalance.Columns.BalanceAccount").Filterable().Sortable())
            .Column(t => t.Type, c => c.Label("Type").LabelKey("CustomerBalance.Columns.Type").Filterable().Sortable())
            .Column(t => t.Amount, c => c.Label("Amount").LabelKey("CustomerBalance.Columns.Amount").Sortable())
            .Column(t => t.Source, c => c.Label("Source").LabelKey("CustomerBalance.Columns.Source").Filterable().Sortable())
            .Column(t => t.Reason, c => c.Label("Reason").LabelKey("CustomerBalance.Columns.Reason").Filterable())
            .Column(t => t.ReferenceType, c => c.Label("Reference Type").LabelKey("CustomerBalance.Columns.ReferenceType").Filterable())
            .Column(t => t.ReferenceId, c => c.Label("Reference ID").LabelKey("CustomerBalance.Columns.ReferenceId").Filterable())
            .Column(t => t.ExpiresAt, c => c.Label("Expires At").LabelKey("CustomerBalance.Columns.ExpiresAt").Sortable())
            .Column(t => t.CreatedAt, c => c.Label("Created At").LabelKey("CustomerBalance.Columns.CreatedAt").Sortable())
            .GlobalSearch(t => t.Reason, t => t.ReferenceType)
            .DateFilter(t => t.CreatedAt)
            .DefaultSort("-createdAt")
            .DefaultPageSize(25);
    }
}
