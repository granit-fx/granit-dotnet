using Granit.Authorization.Domain;
using Granit.DataExchange.Export;

namespace Granit.Authorization.Exports;

public sealed class PermissionGrantExportDefinition : ExportDefinition<PermissionGrant>
{
    public override string Name => "Granit.Authorization.PermissionGrantExport";

    protected override void Configure(ExportDefinitionBuilder<PermissionGrant> builder)
    {
        builder
            .IncludeId()
            .Field(p => p.Name)
            .Field(p => p.ProviderName)
            .Field(p => p.ProviderKey)
            .Field(p => p.TenantId)
            .IncludeAuditFields();
    }
}
