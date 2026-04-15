using Granit.DataExchange.Export;
using Granit.Identity.Local.Domain;

namespace Granit.DataExchange.Definitions.Identity;

public sealed class GranitRoleExportDefinition : ExportDefinition<GranitRole>
{
    public override string Name => "Granit.Identity.GranitRoleExport";

    protected override void Configure(ExportDefinitionBuilder<GranitRole> builder)
    {
        builder
            .IncludeId()
            .Field(r => r.Name)
            .Field(r => r.Description);
    }
}
