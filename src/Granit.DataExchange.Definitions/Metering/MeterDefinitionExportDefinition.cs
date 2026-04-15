using Granit.DataExchange.Export;
using Granit.Metering.Domain;

namespace Granit.DataExchange.Definitions.Metering;

public sealed class MeterDefinitionExportDefinition : ExportDefinition<MeterDefinition>
{
    public override string Name => "Granit.Metering.MeterDefinitionExport";

    protected override void Configure(ExportDefinitionBuilder<MeterDefinition> builder)
    {
        builder
            .IncludeId()
            .Field(m => m.Name)
            .Field(m => m.Unit)
            .Field(m => m.Description)
            .Field(m => m.AggregationType)
            .Field(m => m.IsActive)
            .Field(m => m.TenantId)
            .Field(m => m.CreatedAt, f => f.Format("O"))
            .Field(m => m.CreatedBy)
            .Field(m => m.ModifiedAt, f => f.Format("O"))
            .Field(m => m.ModifiedBy);
    }
}
