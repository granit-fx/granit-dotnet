using Granit.DataExchange.Export;
using Granit.Notifications.Domain;

namespace Granit.Notifications.Exports;

public sealed class NotificationPreferenceExportDefinition : ExportDefinition<NotificationPreference>
{
    public override string Name => "Granit.Notifications.NotificationPreferenceExport";

    protected override void Configure(ExportDefinitionBuilder<NotificationPreference> builder)
    {
        builder
            .IncludeId()
            .Field(e => e.UserId)
            .Field(e => e.NotificationTypeName)
            .Field(e => e.ChannelName)
            .Field(e => e.IsEnabled)
            .Field(e => e.TenantId)
            .Field(e => e.CreatedAt, f => f.Format("O"))
            .Field(e => e.CreatedBy)
            .Field(e => e.ModifiedAt, f => f.Format("O"))
            .Field(e => e.ModifiedBy);
    }
}
