using Granit.DataExchange.Export;
using Granit.Identity.Local.Domain;

namespace Granit.Identity.Local.Exports;

public sealed class GranitUserGroupExportDefinition : ExportDefinition<GranitUserGroup>
{
    public override string Name => "Granit.Identity.Local.GranitUserGroupExport";

    protected override void Configure(ExportDefinitionBuilder<GranitUserGroup> builder)
    {
        builder
            .IncludeId()
            .Field(g => g.Name)
            .Field(g => g.Description)
            .Field(g => g.TenantId)
            .IncludeAuditFields();
    }
}
