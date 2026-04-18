using Granit.QueryEngine;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Internal;

namespace Granit.Subscriptions.Queries;

/// <summary>
/// Query definition for subscriptions — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class SubscriptionQueryDefinition : QueryDefinition<Subscription>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Subscriptions.SubscriptionsQuery";

    /// <inheritdoc/>
    public override Type? LocalizationResourceType => typeof(SubscriptionsLocalizationResource);

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<Subscription> builder)
    {
        builder
            .Column(s => s.TenantId, c => c.Label("Tenant").LabelKey("Subscriptions.Columns.Tenant").Filterable().Sortable())
            .Column(s => s.PlanId, c => c.Label("Plan").LabelKey("Subscriptions.Columns.Plan").Filterable().Sortable())
            .Column(s => s.Status, c => c.Label("Status").LabelKey("Subscriptions.Columns.Status").Filterable().Sortable())
            .Column(s => s.Currency, c => c.Label("Currency").LabelKey("Subscriptions.Columns.Currency").Filterable().Sortable())
            .Column(s => s.CurrentPeriodStart, c => c.Label("Period Start").LabelKey("Subscriptions.Columns.PeriodStart").Sortable())
            .Column(s => s.CurrentPeriodEnd, c => c.Label("Period End").LabelKey("Subscriptions.Columns.PeriodEnd").Sortable())
            .Column(s => s.CancelAtPeriodEnd, c => c.Label("Cancel at Period End").LabelKey("Subscriptions.Columns.CancelAtPeriodEnd").Filterable())
            .Column(s => s.TrialEndsAt, c => c.Label("Trial Ends At").LabelKey("Subscriptions.Columns.TrialEndsAt").Sortable())
            .GlobalSearch(s => s.Currency)
            .DateFilter(s => s.CurrentPeriodStart)
            .DefaultSort("-currentPeriodEnd")
            .DefaultPageSize(25);
    }
}
