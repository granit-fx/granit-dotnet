using Granit.DataExchange.Export;
using Granit.Subscriptions.Domain;

namespace Granit.Subscriptions.Exports;

public sealed class PlanExportDefinition : ExportDefinition<Plan>
{
    public override string Name => "Granit.Subscriptions.PlanExport";

    protected override void Configure(ExportDefinitionBuilder<Plan> builder)
    {
        builder
            .IncludeId()
            .Field(p => p.Name)
            .Field(p => p.Description)
            .Field(p => p.PricingModel)
            .Field(p => p.DefaultInterval)
            .Field(p => p.TrialDays)
            .Field(p => p.SeatLimit)
            .Field(p => p.SortOrder)
            .Field(p => p.LifecycleStatus)
            .Field(p => p.CreatedAt, f => f.Format("O"))
            .Field(p => p.CreatedBy)
            .Field(p => p.ModifiedAt, f => f.Format("O"))
            .Field(p => p.ModifiedBy);
    }
}
