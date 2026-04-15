using Granit.DataExchange.Export;
using Granit.Metering.Domain;

namespace Granit.DataExchange.Definitions.Metering;

public sealed class UsageAggregateExportDefinition : ExportDefinition<UsageAggregate>
{
    public override string Name => "Granit.Metering.UsageAggregateExport";

    protected override void Configure(ExportDefinitionBuilder<UsageAggregate> builder)
    {
        builder
            .IncludeId()
            .Field(u => u.MeterDefinitionId)
            .Field(u => u.Period)
            .Field(u => u.PeriodStart, f => f.Format("O"))
            .Field(u => u.PeriodEnd, f => f.Format("O"))
            .Field(u => u.AggregatedValue, f => f.Format("#,##0.00"))
            .Field(u => u.EventCount)
            .Field(u => u.TenantId);
    }
}
