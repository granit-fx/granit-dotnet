using Granit.QueryEngine;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Internal;

namespace Granit.Webhooks.Queries;

/// <summary>
/// Query definition for webhook subscriptions — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class WebhookSubscriptionQueryDefinition : QueryDefinition<WebhookSubscription>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Webhooks.SubscriptionsQuery";

    /// <inheritdoc/>
    public override Type? LocalizationResourceType => typeof(WebhooksLocalizationResource);

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<WebhookSubscription> builder)
    {
        builder
            .Column(s => s.TenantId, c => c.Label("Tenant").LabelKey("Webhooks.Columns.Tenant").Filterable().Sortable())
            .Column(s => s.EventType, c => c.Label("Event Type").LabelKey("Webhooks.Columns.EventType").Filterable().Sortable())
            // TargetUrl is a HttpsUrl value object (SingleValueObject<string>): mapped by a
            // ValueConverter, so it is display-only here. It is not .Filterable() because the
            // engine cannot build a VO instance from a filter string (an Eq filter would
            // mistranslate, and substring filters cannot translate to LIKE). See issue #2767.
            .Column(s => s.TargetUrl, c => c.Label("Target URL").LabelKey("Webhooks.Columns.TargetUrl"))
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
