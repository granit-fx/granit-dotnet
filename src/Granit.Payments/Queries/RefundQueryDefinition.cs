using Granit.Payments.Domain;
using Granit.QueryEngine;

namespace Granit.Payments.Queries;

/// <summary>
/// Query definition for refunds — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class RefundQueryDefinition : QueryDefinition<Refund>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Payments.RefundQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<Refund> builder)
    {
        builder
            .Column(e => e.Status, c => c.Label("Status").LabelKey("Payments.Columns.Status").Filterable().Sortable())
            .Column(e => e.Amount, c => c.Label("Amount").LabelKey("Payments.Columns.Amount").Sortable())
            .Column(e => e.Currency, c => c.Label("Currency").LabelKey("Payments.Columns.Currency").Filterable().Sortable())
            .Column(e => e.ProviderRefundId, c => c.Label("Provider Refund ID").LabelKey("Payments.Columns.ProviderRefundId").Filterable())
            .Column(e => e.Reason, c => c.Label("Reason").LabelKey("Payments.Columns.Reason").Filterable())
            .Column(e => e.CreatedAt, c => c.Label("Created At").LabelKey("Payments.Columns.CreatedAt").Sortable())
            .Column(e => e.CompletedAt, c => c.Label("Completed At").LabelKey("Payments.Columns.CompletedAt").Sortable())
            .GlobalSearch(e => e.ProviderRefundId, e => e.Reason, e => e.Currency)
            .DateFilter(e => e.CreatedAt)
            .DefaultSort("-createdAt")
            .DefaultPageSize(25);
    }
}
