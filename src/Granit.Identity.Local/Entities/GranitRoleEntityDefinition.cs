using Granit.Entities;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Exports;
using Granit.Identity.Local.Queries;

namespace Granit.Identity.Local.Entities;

/// <summary>
/// Phase 2 EntityDefinition for the local <see cref="GranitRole"/> aggregate
/// (extends <c>IdentityRole&lt;Guid&gt;</c>). Composes the existing
/// <see cref="GranitRoleQueryDefinition"/> + <see cref="GranitRoleExportDefinition"/>
/// into the unified manifest surface so the admin grid renders without
/// per-form scaffolding (per ADR-040 / ADR-050).
/// </summary>
public sealed class GranitRoleEntityDefinition : EntityDefinition<GranitRole>
{
    /// <inheritdoc />
    public override string Name => "Granit.Identity.Local.GranitRole";

    /// <inheritdoc />
    protected override void Configure(EntityDefinitionBuilder<GranitRole> builder) =>
        builder
            .DisplayKey("IdentityLocal:Entity.Role")
            .Icon("shield-check")
            .PermissionGroup("IdentityLocal.Roles")
            .DisplayProperty(r => r.Name)
            .SubtitleProperty(r => r.Description)
            .Query<GranitRoleQueryDefinition>()
            .Export<GranitRoleExportDefinition>()
            .Form("default", f => f
                .Section("identity", s => s
                    .Field(r => r.Name)
                    .Field(r => r.Description)))
            .Detail("default", d => d.Section("overview", s => s.InheritsFromForm("default")));
}
