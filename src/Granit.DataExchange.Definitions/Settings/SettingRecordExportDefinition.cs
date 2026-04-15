using Granit.DataExchange.Export;
using Granit.Settings.EntityFrameworkCore.Entities;

namespace Granit.DataExchange.Definitions.Settings;

public sealed class SettingRecordExportDefinition : ExportDefinition<SettingRecord>
{
    public override string Name => "Granit.Settings.SettingRecordExport";

    protected override void Configure(ExportDefinitionBuilder<SettingRecord> builder)
    {
        builder
            .IncludeId()
            .Field(e => e.Name)
            .Field(e => e.ProviderName)
            .Field(e => e.ProviderKey)
            .Field(e => e.Value)
            .Field(e => e.CreatedAt, f => f.Format("O"))
            .Field(e => e.CreatedBy)
            .Field(e => e.ModifiedAt, f => f.Format("O"))
            .Field(e => e.ModifiedBy);
    }
}
