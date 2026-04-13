using Granit.QueryEngine;
using Granit.Webhooks.Domain;

namespace Granit.Webhooks.Endpoints.Queries;

/// <summary>
/// Query definition for webhook delivery attempts — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class WebhookDeliveryAttemptQueryDefinition : QueryDefinition<WebhookDeliveryAttempt>
{
    /// <inheritdoc/>
    public override string Name => "Webhooks.DeliveryAttempts";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<WebhookDeliveryAttempt> builder)
    {
        builder
            .Column(d => d.SubscriptionId, c => c.Label("Subscription").Filterable().Sortable())
            .Column(d => d.TenantId, c => c.Label("Tenant").Filterable().Sortable())
            .Column(d => d.EventType, c => c.Label("Event Type").Filterable().Sortable())
            .Column(d => d.TargetUrl, c => c.Label("Target URL").Filterable())
            .Column(d => d.HttpStatusCode, c => c.Label("HTTP Status").Filterable().Sortable())
            .Column(d => d.IsSuccess, c => c.Label("Success").Filterable().Sortable())
            .Column(d => d.OccurredAt, c => c.Label("Occurred At").Sortable())
            .Column(d => d.DurationMs, c => c.Label("Duration (ms)").Sortable())
            .Column(d => d.ErrorMessage, c => c.Label("Error Message"))
            .GlobalSearch(d => d.EventType, d => d.TargetUrl)
            .DateFilter(d => d.OccurredAt)
            .DefaultSort("-occurredAt")
            .DefaultPageSize(25);
    }
}
