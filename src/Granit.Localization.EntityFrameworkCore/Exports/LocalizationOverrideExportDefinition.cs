using Granit.DataExchange.Export;
using Granit.Localization.EntityFrameworkCore.Entities;

namespace Granit.Localization.EntityFrameworkCore.Exports;

public sealed class LocalizationOverrideExportDefinition : ExportDefinition<LocalizationOverride>
{
    public override string Name => "Granit.Localization.LocalizationOverrideExport";

    protected override void Configure(ExportDefinitionBuilder<LocalizationOverride> builder)
    {
        builder
            .IncludeId()
            .Field(e => e.ResourceName)
            .Field(e => e.CultureName)
            .Field(e => e.Key)
            .Field(e => e.Value)
            .Field(e => e.TenantId)
            .Field(e => e.CreatedAt, f => f.Format("O"))
            .Field(e => e.CreatedBy)
            .Field(e => e.ModifiedAt, f => f.Format("O"))
            .Field(e => e.ModifiedBy);
    }
}
