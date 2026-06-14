using Granit.DataExchange.Export;
using Granit.Localization.Domain;

namespace Granit.Localization.Exports;

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
            .IncludeAuditFields();
    }
}
