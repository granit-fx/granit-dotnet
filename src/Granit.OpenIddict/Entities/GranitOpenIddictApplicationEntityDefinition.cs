using Granit.Entities;
using Granit.OpenIddict.Exports;
using Granit.OpenIddict.Models;
using Granit.OpenIddict.Queries;

namespace Granit.OpenIddict.Entities;

/// <summary>
/// Phase 2 EntityDefinition for <c>GranitOpenIddictApplication</c> —
/// an OAuth2 / OIDC client application registration. Composes the existing
/// <see cref="GranitOpenIddictApplicationQueryDefinition"/> +
/// <see cref="OpenIddictApplicationExportDefinition"/> into the unified
/// manifest surface so the OpenIddict admin UI renders without per-form
/// scaffolding (per ADR-040 / ADR-050).
/// </summary>
public sealed class GranitOpenIddictApplicationEntityDefinition : EntityDefinition<OpenIddictApplicationModel>
{
    /// <inheritdoc />
    public override string Name => "Granit.OpenIddict.Application";

    /// <inheritdoc />
    protected override void Configure(EntityDefinitionBuilder<OpenIddictApplicationModel> builder) =>
        builder
            .DisplayKey("OpenIddict:Entity.Application")
            .Icon("key")
            .PermissionGroup("OpenIddict.Applications")
            .DisplayProperty(a => a.DisplayName)
            .SubtitleProperty(a => a.ClientId)
            .Query<GranitOpenIddictApplicationQueryDefinition>()
            .Export<OpenIddictApplicationExportDefinition>()
            .Form("default", f => f
                .Section("identity", s => s
                    .Field(a => a.ClientId)
                    .Field(a => a.DisplayName)
                    .Field(a => a.ClientType)
                    .Field(a => a.ConsentType)
                    .Field(a => a.ApplicationType)))
            .Detail("default", d => d.Section("overview", s => s.InheritsFromForm("default")));
}
