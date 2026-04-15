using Granit.QueryEngine;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Endpoints.Internal;

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
    public override Type? LocalizationResourceType => typeof(WebhooksEndpointsLocalizationResource);

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<WebhookSubscription> builder)
    {
        builder
            .Column(s => s.TenantId, c => c.Label("Tenant").LabelKey("Webhooks.Columns.Tenant").Filterable().Sortable())
            .Column(s => s.EventType, c => c.Label("Event Type").LabelKey("Webhooks.Columns.EventType").Filterable().Sortable())
            .Column(s => s.TargetUrl, c => c.Label("Target URL").LabelKey("Webhooks.Columns.TargetUrl").Filterable())
            .Column(s => s.Status, c => c.Label("Status").LabelKey("Webhooks.Columns.Status").Filterable().Sortable())
            .Column(s => s.ConsecutiveFailureCount, c => c.Label("Consecutive Failures").LabelKey("Webhooks.Columns.ConsecutiveFailures").Sortable())
            .Column(s => s.LastSuccessAt, c => c.Label("Last Success At").LabelKey("Webhooks.Columns.LastSuccessAt").Sortable())
            .Column(s => s.SuspendedAt, c => c.Label("Suspended At").LabelKey("Webhooks.Columns.SuspendedAt").Sortable())
            .Column(s => s.SuspendedBy, c => c.Label("Suspended By").LabelKey("Webhooks.Columns.SuspendedBy").Filterable())
            .GlobalSearch(s => s.EventType)
            .DefaultSort("-lastSuccessAt")
            .DefaultPageSize(25);
    }
}
