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
            // Identity
            .Column(s => s.TenantId, c => c.Label("Tenant").LabelKey("Subscriptions.Columns.Tenant").Filterable().Sortable())
            .Column(s => s.PartyId, c => c.Label("Party").LabelKey("Subscriptions.Columns.Party").Filterable().Sortable())
            .Column(s => s.PlanId, c => c.Label("Plan").LabelKey("Subscriptions.Columns.Plan").Filterable().Sortable())
            .Column(s => s.PlanPriceId, c => c.Label("Plan Price").LabelKey("Subscriptions.Columns.PlanPrice").Filterable())
            .Column(s => s.Status, c => c.Label("Status").LabelKey("Subscriptions.Columns.Status").Filterable().Sortable())
            .Column(s => s.Currency, c => c.Label("Currency").LabelKey("Subscriptions.Columns.Currency").Filterable().Sortable())
            // Billing cycle
            .Column(s => s.CurrentPeriodStart, c => c.Label("Period Start").LabelKey("Subscriptions.Columns.PeriodStart").Sortable())
            .Column(s => s.CurrentPeriodEnd, c => c.Label("Period End").LabelKey("Subscriptions.Columns.PeriodEnd").Sortable())
            .Column(s => s.BillingCycleAnchor, c => c.Label("Billing Cycle Anchor").LabelKey("Subscriptions.Columns.BillingCycleAnchor").Sortable())
            // Trial / cancellation
            .Column(s => s.TrialEndsAt, c => c.Label("Trial Ends At").LabelKey("Subscriptions.Columns.TrialEndsAt").Sortable())
            .Column(s => s.CancelAtPeriodEnd, c => c.Label("Cancel at Period End").LabelKey("Subscriptions.Columns.CancelAtPeriodEnd").Filterable())
            .Column(s => s.CancelledAt, c => c.Label("Cancelled At").LabelKey("Subscriptions.Columns.CancelledAt").Sortable())
            .Column(s => s.CancellationReason, c => c.Label("Cancellation Reason").LabelKey("Subscriptions.Columns.CancellationReason").Filterable())
            // Dunning
            .Column(s => s.DunningAttempt, c => c.Label("Dunning Attempt").LabelKey("Subscriptions.Columns.DunningAttempt").Filterable().Sortable())
            // Audit
            .Column(s => s.CreatedAt, c => c.Label("Created").LabelKey("Subscriptions.Columns.CreatedAt").Sortable())
            .Column(s => s.ModifiedAt, c => c.Label("Modified").LabelKey("Subscriptions.Columns.ModifiedAt").Sortable())
            .GlobalSearch(s => s.Currency, s => s.CancellationReason)
            .DateFilter(s => s.CurrentPeriodStart)
            .DefaultSort("-currentPeriodEnd")
            .DefaultPageSize(25);
    }
}
