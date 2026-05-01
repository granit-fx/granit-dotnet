using Granit.Entities;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Exports;
using Granit.Identity.Local.Queries;

namespace Granit.Identity.Local.Entities;

/// <summary>
/// Phase 2 EntityDefinition for <see cref="GranitUserGroup"/>. Composes the
/// existing <see cref="GranitUserGroupQueryDefinition"/> +
/// <see cref="GranitUserGroupExportDefinition"/> into the unified manifest
/// surface (per ADR-040 / ADR-050).
/// </summary>
public sealed class GranitUserGroupEntityDefinition : EntityDefinition<GranitUserGroup>
{
    /// <inheritdoc />
    public override string Name => "Granit.Identity.Local.GranitUserGroup";

    /// <inheritdoc />
    protected override void Configure(EntityDefinitionBuilder<GranitUserGroup> builder) =>
        builder
            .DisplayKey("IdentityLocal:Entity.UserGroup")
            .Icon("users")
            .PermissionGroup("IdentityLocal.UserGroups")
            .DisplayProperty(g => g.Name)
            .SubtitleProperty(g => g.Description)
            .Query<GranitUserGroupQueryDefinition>()
            .Export<GranitUserGroupExportDefinition>()
            .Form("default", f => f
                .Section("identity", s => s
                    .Field(g => g.Name)
                    .Field(g => g.Description)))
            .Detail("default", d =>
            {
                d.Section("overview", s => s.InheritsFromForm("default"));
                d.SidePanel.Audit().Timeline();
            });
}
