using Granit.DataExchange.Export;
using Granit.Notifications.Domain;

namespace Granit.Notifications.Exports;

public sealed class UserNotificationExportDefinition : ExportDefinition<UserNotification>
{
    public override string Name => "Granit.Notifications.UserNotificationExport";

    protected override void Configure(ExportDefinitionBuilder<UserNotification> builder)
    {
        builder
            .IncludeId()
            .Field(e => e.NotificationId)
            .Field(e => e.NotificationTypeName)
            .Field(e => e.Severity)
            .Field(e => e.RecipientUserId)
            .Field(e => e.State)
            .Field(e => e.CreatedAt, f => f.Format("O"))
            .Field(e => e.ReadAt, f => f.Format("O"))
            .Field(e => e.RelatedEntityType)
            .Field(e => e.RelatedEntityId)
            .Field(e => e.TenantId);
    }
}
