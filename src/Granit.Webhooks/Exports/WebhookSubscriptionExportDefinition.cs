using Granit.DataExchange.Export;
using Granit.Webhooks.Domain;

namespace Granit.Webhooks.Exports;

public sealed class WebhookSubscriptionExportDefinition : ExportDefinition<WebhookSubscription>
{
    public override string Name => "Granit.Webhooks.WebhookSubscriptionExport";

    protected override void Configure(ExportDefinitionBuilder<WebhookSubscription> builder)
    {
        builder
            .IncludeId()
            .Field(e => e.EventType)
            .Field(e => e.TargetUrl)
            .Field(e => e.Status)
            .Field(e => e.SigningSecretHint)
            .Field(e => e.DeactivationReason)
            .Field(e => e.ConsecutiveFailureCount)
            .Field(e => e.LastSuccessAt, f => f.Format("O"))
            .Field(e => e.SuspendedAt, f => f.Format("O"))
            .Field(e => e.SuspendedBy)
            .Field(e => e.TenantId)
            .Field(e => e.CreatedAt, f => f.Format("O"))
            .Field(e => e.CreatedBy)
            .Field(e => e.ModifiedAt, f => f.Format("O"))
            .Field(e => e.ModifiedBy)
            .ComplexField("SigningKeys", e => e.SigningKeys
                .Select(k => new WebhookSigningKeySnapshot(k.Id, k.Status, k.CreatedAt, k.ExpiresAt, k.RevokedAt))
                .ToList());
    }
}
