using Granit.DataExchange.Export;
using Granit.Webhooks.Domain;

namespace Granit.Webhooks.Exports;

public sealed class WebhookDeliveryAttemptExportDefinition : ExportDefinition<WebhookDeliveryAttempt>
{
    public override string Name => "Granit.Webhooks.WebhookDeliveryAttemptExport";

    protected override void Configure(ExportDefinitionBuilder<WebhookDeliveryAttempt> builder)
    {
        builder
            .IncludeId()
            .Field(e => e.DeliveryId)
            .Field(e => e.SubscriptionId)
            .Field(e => e.EventType)
            .Field(e => e.TargetUrl)
            .Field(e => e.HttpStatusCode)
            .Field(e => e.PayloadHash)
            .Field(e => e.OccurredAt, f => f.Format("O"))
            .Field(e => e.DurationMs)
            .Field(e => e.ErrorMessage)
            .Field(e => e.IsSuccess)
            .Field(e => e.TenantId);
    }
}
