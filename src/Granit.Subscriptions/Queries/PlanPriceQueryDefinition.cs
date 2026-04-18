using Granit.QueryEngine;
using Granit.Subscriptions.Domain;

namespace Granit.Subscriptions.Queries;

/// <summary>
/// Query definition for subscription plan prices — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class PlanPriceQueryDefinition : QueryDefinition<PlanPrice>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Subscriptions.PlanPriceQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<PlanPrice> builder)
    {
        builder
            .Column(p => p.Amount, c => c.Label("Amount").LabelKey("Subscriptions.Columns.Amount").Sortable())
            .Column(p => p.Currency, c => c.Label("Currency").LabelKey("Subscriptions.Columns.Currency").Filterable().Sortable())
            .Column(p => p.Interval, c => c.Label("Interval").LabelKey("Subscriptions.Columns.Interval").Filterable().Sortable())
            .Column(p => p.IsCurrent, c => c.Label("Current").LabelKey("Subscriptions.Columns.IsCurrent").Filterable().Sortable())
            .Column(p => p.EffectiveFrom, c => c.Label("Effective From").LabelKey("Subscriptions.Columns.EffectiveFrom").Sortable())
            .Column(p => p.ReplacedAt, c => c.Label("Replaced At").LabelKey("Subscriptions.Columns.ReplacedAt").Sortable())
            .GlobalSearch(p => p.Currency)
            .DateFilter(p => p.EffectiveFrom)
            .DefaultSort("-effectiveFrom")
            .DefaultPageSize(25);
    }
}
