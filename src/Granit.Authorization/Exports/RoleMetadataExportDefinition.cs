using Granit.Authorization.Domain;
using Granit.DataExchange.Export;

namespace Granit.Authorization.Exports;

public sealed class RoleMetadataExportDefinition : ExportDefinition<RoleMetadata>
{
    public override string Name => "Granit.Authorization.RoleMetadataExport";

    protected override void Configure(ExportDefinitionBuilder<RoleMetadata> builder)
    {
        builder
            .IncludeId()
            .Field(r => r.Name)
            .Field(r => r.MultiTenancySides)
            .Field(r => r.TenantId)
            .Field(r => r.ClientId)
            .Field(r => r.Description)
            .Field(r => r.IsSystem)
            .Field(r => r.IsOrphaned)
            .Field(r => r.OrphanedAt, f => f.Format("O"))
            .Field(r => r.CreatedAt, f => f.Format("O"))
            .Field(r => r.CreatedBy)
            .Field(r => r.ModifiedAt, f => f.Format("O"))
            .Field(r => r.ModifiedBy);
    }
}
