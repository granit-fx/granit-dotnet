using Granit.DataExchange.Export;
using Granit.Subscriptions.Domain;

namespace Granit.DataExchange.Definitions.Subscriptions;

public sealed class PlanPriceExportDefinition : ExportDefinition<PlanPrice>
{
    public override string Name => "Granit.Subscriptions.PlanPriceExport";

    protected override void Configure(ExportDefinitionBuilder<PlanPrice> builder)
    {
        builder
            .IncludeId()
            .Field(p => p.Amount, f => f.Format("#,##0.00"))
            .Field(p => p.Currency)
            .Field(p => p.Interval)
            .Field(p => p.EffectiveFrom, f => f.Format("O"))
            .Field(p => p.ReplacedByPriceId)
            .Field(p => p.ReplacedAt, f => f.Format("O"))
            .Field(p => p.IsActive);
    }
}
