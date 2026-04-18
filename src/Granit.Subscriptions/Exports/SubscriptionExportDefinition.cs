using Granit.DataExchange.Export;
using Granit.Subscriptions.Domain;

namespace Granit.Subscriptions.Exports;

public sealed class SubscriptionExportDefinition : ExportDefinition<Subscription>
{
    public override string Name => "Granit.Subscriptions.SubscriptionExport";

    protected override void Configure(ExportDefinitionBuilder<Subscription> builder)
    {
        builder
            .IncludeId()
            .Field(s => s.PlanId)
            .Field(s => s.Status)
            .Field(s => s.CurrentPeriodStart, f => f.Format("O"))
            .Field(s => s.CurrentPeriodEnd, f => f.Format("O"))
            .Field(s => s.BillingCycleAnchor, f => f.Format("O"))
            .Field(s => s.TrialEndsAt, f => f.Format("O"))
            .Field(s => s.CancelAtPeriodEnd)
            .Field(s => s.CancelledAt, f => f.Format("O"))
            .Field(s => s.CancellationReason)
            .Field(s => s.Currency)
            .Field(s => s.PlanPriceId)
            .Field(s => s.DunningAttempt)
            .Field(s => s.TenantId)
            .Field(s => s.CreatedAt, f => f.Format("O"))
            .Field(s => s.CreatedBy)
            .Field(s => s.ModifiedAt, f => f.Format("O"))
            .Field(s => s.ModifiedBy);
    }
}
