using Granit.QueryEngine;
using Granit.Webhooks.Domain;

namespace Granit.Webhooks.Endpoints.Queries;

/// <summary>
/// Query definition for webhook subscriptions — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class WebhookSubscriptionQueryDefinition : QueryDefinition<WebhookSubscription>
{
    /// <inheritdoc/>
    public override string Name => "Webhooks.Subscriptions";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<WebhookSubscription> builder)
    {
        builder
            .Column(s => s.TenantId, c => c.Label("Tenant").Filterable().Sortable())
            .Column(s => s.EventType, c => c.Label("Event Type").Filterable().Sortable())
            .Column(s => s.TargetUrl, c => c.Label("Target URL").Filterable())
            .Column(s => s.Status, c => c.Label("Status").Filterable().Sortable())
            .Column(s => s.ConsecutiveFailureCount, c => c.Label("Consecutive Failures").Sortable())
            .Column(s => s.LastSuccessAt, c => c.Label("Last Success At").Sortable())
            .Column(s => s.SuspendedAt, c => c.Label("Suspended At").Sortable())
            .Column(s => s.SuspendedBy, c => c.Label("Suspended By").Filterable())
            .GlobalSearch(s => s.EventType)
            .DefaultSort("-lastSuccessAt")
            .DefaultPageSize(25);
    }
}
