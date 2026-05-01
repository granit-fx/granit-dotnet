using Granit.Authorization.Domain;
using Granit.Authorization.Exports;
using Granit.Authorization.Queries;
using Granit.Entities;

namespace Granit.Authorization.Entities;

/// <summary>
/// Phase 2 EntityDefinition for <see cref="RoleMetadata"/> — the authorization-side
/// projection of a role (its declared <c>MultiTenancySides</c>, system flag, ownership
/// metadata) that lives independently from <c>GranitRole</c> in <c>Granit.Identity.Local</c>.
/// Composes the existing <see cref="RoleMetadataQueryDefinition"/> +
/// <see cref="RoleMetadataExportDefinition"/> into the unified manifest surface.
/// </summary>
public sealed class RoleMetadataEntityDefinition : EntityDefinition<RoleMetadata>
{
    /// <inheritdoc />
    public override string Name => "Granit.Authorization.RoleMetadata";

    /// <inheritdoc />
    protected override void Configure(EntityDefinitionBuilder<RoleMetadata> builder) =>
        builder
            .DisplayKey("Authorization:Entity.RoleMetadata")
            .Icon("shield-check")
            .PermissionGroup("Authorization.Definitions")
            .DisplayProperty(r => r.Name)
            .SubtitleProperty(r => r.MultiTenancySides)
            .Query<RoleMetadataQueryDefinition>()
            .Export<RoleMetadataExportDefinition>()
            .Form("default", f => f
                .Section("identity", s => s
                    .Field(r => r.Name)
                    .Field(r => r.Description)
                    .Field(r => r.MultiTenancySides)
                    .Field(r => r.IsSystem, x => x.ReadOnly())))
            .Detail("default", d =>
            {
                d.Section("overview", s => s.InheritsFromForm("default"));
                d.SidePanel.Audit().Timeline();
            });
}
