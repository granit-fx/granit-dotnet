using Granit.QueryEngine;
using Granit.QueryEngine.Filtering;
using Granit.Subscriptions.Domain;

namespace Granit.Subscriptions.Endpoints.Queries;

/// <summary>
/// Query definition for subscriptions — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class SubscriptionQueryDefinition : QueryDefinition<Subscription>
{
    /// <inheritdoc/>
    public override string Name => "Subscriptions.Subscriptions";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<Subscription> builder)
    {
        builder
            .Column(s => s.TenantId, c => c.Label("Tenant").Filterable().Sortable())
            .Column(s => s.PlanId, c => c.Label("Plan").Filterable().Sortable())
            .Column(s => s.Status, c => c.Label("Status").Filterable().Sortable())
            .Column(s => s.Currency, c => c.Label("Currency").Filterable().Sortable())
            .Column(s => s.CurrentPeriodStart, c => c.Label("Period Start").Sortable())
            .Column(s => s.CurrentPeriodEnd, c => c.Label("Period End").Sortable())
            .Column(s => s.CancelAtPeriodEnd, c => c.Label("Cancel at Period End").Filterable())
            .Column(s => s.TrialEndsAt, c => c.Label("Trial Ends At").Sortable())
            .GlobalSearch(s => s.Currency)
            .DateFilter(s => s.CurrentPeriodStart)
            .DefaultSort("-currentPeriodEnd")
            .DefaultPageSize(25);
    }
}
