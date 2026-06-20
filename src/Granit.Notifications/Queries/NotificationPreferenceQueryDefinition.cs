using Granit.Notifications.Domain;
using Granit.QueryEngine;

namespace Granit.Notifications.Queries;

/// <summary>
/// Query definition for notification preferences — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class NotificationPreferenceQueryDefinition : QueryDefinition<NotificationPreference>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Notifications.NotificationPreferenceQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<NotificationPreference> builder)
    {
        builder
            .Column(e => e.TenantId, c => c.Label("Tenant").LabelKey("Notifications.Columns.Tenant").Filterable().Sortable())
            .Column(e => e.UserId, c => c.Label("User").LabelKey("Notifications.Columns.UserId").Filterable().Sortable().Lookup("users", requiredPermission: "Identity.Users.Read"))
            .Column(e => e.NotificationTypeName, c => c.Label("Notification Type").LabelKey("Notifications.Columns.NotificationTypeName").Filterable().Sortable())
            .Column(e => e.ChannelName, c => c.Label("Channel").LabelKey("Notifications.Columns.ChannelName").Filterable().Sortable())
            .Column(e => e.IsEnabled, c => c.Label("Enabled").LabelKey("Notifications.Columns.IsEnabled").Filterable().Sortable())
            .Column(e => e.CreatedAt, c => c.Label("Created At").LabelKey("Notifications.Columns.CreatedAt").Sortable())
            .Column(e => e.ModifiedAt, c => c.Label("Modified At").LabelKey("Notifications.Columns.ModifiedAt").Sortable())
            .GlobalSearch(e => e.UserId, e => e.NotificationTypeName)
            .DateFilter(e => e.CreatedAt)
            .DefaultSort("-createdAt")
            .DefaultPageSize(25);
    }
}
