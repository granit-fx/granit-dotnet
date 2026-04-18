using Granit.DataExchange.Export;
using Granit.OpenIddict.Entities.OpenIddict;

namespace Granit.OpenIddict.Exports;

public sealed class OpenIddictApplicationExportDefinition : ExportDefinition<GranitOpenIddictApplication>
{
    public override string Name => "Granit.OpenIddict.ApplicationExport";

    protected override void Configure(ExportDefinitionBuilder<GranitOpenIddictApplication> builder)
    {
        builder
            .IncludeId()
            .Field(a => a.ClientId)
            .Field(a => a.DisplayName)
            .Field(a => a.ClientType)
            .Field(a => a.ConsentType)
            .Field(a => a.ApplicationType)
            .Field(a => a.TenantId);
    }
}
