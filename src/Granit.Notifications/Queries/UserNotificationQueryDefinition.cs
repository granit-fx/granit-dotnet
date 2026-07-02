using Granit.Notifications.Domain;
using Granit.QueryEngine;

namespace Granit.Notifications.Queries;

/// <summary>
/// Query definition for user notifications — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class UserNotificationQueryDefinition : QueryDefinition<UserNotification>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Notifications.UserNotificationQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<UserNotification> builder)
    {
        builder
            .Column(e => e.TenantId, c => c.Label("Tenant").LabelKey("Notifications.Columns.Tenant").Filterable().Sortable())
            .Column(e => e.RecipientUserId, c => c.Label("Recipient").LabelKey("Notifications.Columns.RecipientUserId").Filterable().Sortable().Lookup("users", requiredPermission: "Identity.Users.Read"))
            .Column(e => e.NotificationTypeName, c => c.Label("Notification Type").LabelKey("Notifications.Columns.NotificationTypeName").Filterable().Sortable())
            .Column(e => e.Severity, c => c.Label("Severity").LabelKey("Notifications.Columns.Severity").Filterable().Sortable())
            .Column(e => e.State, c => c.Label("State").LabelKey("Notifications.Columns.State").Filterable().Sortable())
            .Column(e => e.RelatedEntityType, c => c.Label("Related Entity Type").LabelKey("Notifications.Columns.RelatedEntityType").Filterable())
            .Column(e => e.CreatedAt, c => c.Label("Created At").LabelKey("Notifications.Columns.CreatedAt").Sortable())
            .Column(e => e.ReadAt, c => c.Label("Read At").LabelKey("Notifications.Columns.ReadAt").Sortable())
            .AllowGroupBy(e => e.Severity)
            .AllowGroupBy(e => e.State)
            .GlobalSearch(e => e.RecipientUserId, e => e.NotificationTypeName)
            .DateFilter(e => e.CreatedAt)
            .DefaultSort("-createdAt")
            .DefaultPageSize(25);
    }
}
