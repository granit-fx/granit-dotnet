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
            .Field(g => g.CreatedAt, f => f.Format("O"))
            .Field(g => g.CreatedBy)
            .Field(g => g.ModifiedAt, f => f.Format("O"))
            .Field(g => g.ModifiedBy);
    }
}
