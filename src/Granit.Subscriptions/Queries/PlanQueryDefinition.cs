using Granit.QueryEngine;
using Granit.Subscriptions.Domain;

namespace Granit.Subscriptions.Queries;

/// <summary>
/// Query definition for subscription plans — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class PlanQueryDefinition : QueryDefinition<Plan>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Subscriptions.PlanQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<Plan> builder)
    {
        builder
            .Column(p => p.Name, c => c.Label("Name").LabelKey("Subscriptions.Columns.PlanName").Filterable().Sortable())
            .Column(p => p.Description, c => c.Label("Description").LabelKey("Subscriptions.Columns.Description").Filterable())
            .Column(p => p.PricingModel, c => c.Label("Pricing Model").LabelKey("Subscriptions.Columns.PricingModel").Filterable().Sortable())
            .Column(p => p.DefaultInterval, c => c.Label("Default Interval").LabelKey("Subscriptions.Columns.DefaultInterval").Filterable().Sortable())
            .Column(p => p.LifecycleStatus, c => c.Label("Lifecycle Status").LabelKey("Subscriptions.Columns.LifecycleStatus").Filterable().Sortable())
            .Column(p => p.TrialDays, c => c.Label("Trial Days").LabelKey("Subscriptions.Columns.TrialDays").Sortable())
            .Column(p => p.SeatLimit, c => c.Label("Seat Limit").LabelKey("Subscriptions.Columns.SeatLimit").Sortable())
            .Column(p => p.SortOrder, c => c.Label("Sort Order").LabelKey("Subscriptions.Columns.SortOrder").Sortable())
            .Column(p => p.CreatedAt, c => c.Label("Created At").LabelKey("Subscriptions.Columns.CreatedAt").Sortable())
            .Column(p => p.ModifiedAt, c => c.Label("Modified At").LabelKey("Subscriptions.Columns.ModifiedAt").Sortable())
            .GlobalSearch(p => p.Name, p => p.Description)
            .DateFilter(p => p.CreatedAt)
            .DefaultSort("sortOrder")
            .DefaultPageSize(25);
    }
}
