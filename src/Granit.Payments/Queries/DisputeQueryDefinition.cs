using Granit.Payments.Domain;
using Granit.QueryEngine;

namespace Granit.Payments.Queries;

/// <summary>
/// Query definition for payment disputes — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class DisputeQueryDefinition : QueryDefinition<Dispute>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Payments.DisputeQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<Dispute> builder)
    {
        builder
            .Column(e => e.ProviderDisputeId, c => c.Label("Provider Dispute ID").LabelKey("Payments.Columns.ProviderDisputeId").Filterable().Sortable())
            .Column(e => e.Status, c => c.Label("Status").LabelKey("Payments.Columns.Status").Filterable().Sortable())
            .Column(e => e.Reason, c => c.Label("Reason").LabelKey("Payments.Columns.Reason").Filterable().Sortable())
            .Column(e => e.Amount, c => c.Label("Amount").LabelKey("Payments.Columns.Amount").Sortable())
            .Column(e => e.Currency, c => c.Label("Currency").LabelKey("Payments.Columns.Currency").Filterable().Sortable())
            .Column(e => e.CreatedAt, c => c.Label("Created At").LabelKey("Payments.Columns.CreatedAt").Sortable())
            .Column(e => e.ResolvedAt, c => c.Label("Resolved At").LabelKey("Payments.Columns.ResolvedAt").Sortable())
            .GlobalSearch(e => e.ProviderDisputeId, e => e.Reason)
            .DateFilter(e => e.CreatedAt)
            .DefaultSort("-createdAt")
            .DefaultPageSize(25);
    }
}
