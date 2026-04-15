using Granit.QueryEngine;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Endpoints.Internal;

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
    public override Type? LocalizationResourceType => typeof(WebhooksEndpointsLocalizationResource);

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<WebhookDeliveryAttempt> builder)
    {
        builder
            .Column(d => d.SubscriptionId, c => c.Label("Subscription").LabelKey("Webhooks.Columns.Subscription").Filterable().Sortable())
            .Column(d => d.TenantId, c => c.Label("Tenant").LabelKey("Webhooks.Columns.Tenant").Filterable().Sortable())
            .Column(d => d.EventType, c => c.Label("Event Type").LabelKey("Webhooks.Columns.EventType").Filterable().Sortable())
            .Column(d => d.TargetUrl, c => c.Label("Target URL").LabelKey("Webhooks.Columns.TargetUrl").Filterable())
            .Column(d => d.HttpStatusCode, c => c.Label("HTTP Status").LabelKey("Webhooks.Columns.HttpStatus").Filterable().Sortable())
            .Column(d => d.IsSuccess, c => c.Label("Success").LabelKey("Webhooks.Columns.Success").Filterable().Sortable())
            .Column(d => d.OccurredAt, c => c.Label("Occurred At").LabelKey("Webhooks.Columns.OccurredAt").Sortable())
            .Column(d => d.DurationMs, c => c.Label("Duration (ms)").LabelKey("Webhooks.Columns.DurationMs").Sortable())
            .Column(d => d.ErrorMessage, c => c.Label("Error Message").LabelKey("Webhooks.Columns.ErrorMessage"))
            .GlobalSearch(d => d.EventType, d => d.TargetUrl)
            .DateFilter(d => d.OccurredAt)
            .DefaultSort("-occurredAt")
            .DefaultPageSize(25);
    }
}
