using Granit.Authorization.Domain;
using Granit.Authorization.Exports;
using Granit.Authorization.Queries;
using Granit.Entities;

namespace Granit.Authorization.Entities;

/// <summary>
/// Phase 2 EntityDefinition for <see cref="PermissionGrant"/> — a single grant
/// row tying a permission to a user / role / client. Composes the existing
/// <see cref="PermissionGrantQueryDefinition"/> + <see cref="PermissionGrantExportDefinition"/>
/// into the unified manifest surface.
/// </summary>
public sealed class PermissionGrantEntityDefinition : EntityDefinition<PermissionGrant>
{
    /// <inheritdoc />
    public override string Name => "Granit.Authorization.PermissionGrant";

    /// <inheritdoc />
    protected override void Configure(EntityDefinitionBuilder<PermissionGrant> builder) =>
        builder
            .DisplayKey("Authorization:Entity.PermissionGrant")
            .Icon("key")
            .PermissionGroup("Authorization.Grants")
            .DisplayProperty(p => p.Name)
            .SubtitleProperty(p => p.ProviderKey)
            .Query<PermissionGrantQueryDefinition>()
            .Export<PermissionGrantExportDefinition>()
            .Form("default", f => f
                .Section("identity", s => s
                    .Field(p => p.Name)
                    .Field(p => p.ProviderName)
                    .Field(p => p.ProviderKey)))
            .Detail("default", d =>
            {
                d.Section("overview", s => s.InheritsFromForm("default"));
                d.SidePanel.Audit().Timeline();
            });
}
