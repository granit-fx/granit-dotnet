using Granit.Entities;
using Granit.OpenIddict.Entities.OpenIddict;
using Granit.OpenIddict.Exports;
using Granit.OpenIddict.Queries;

namespace Granit.OpenIddict.Entities;

/// <summary>
/// Phase 2 EntityDefinition for <see cref="GranitOpenIddictScope"/> — an OAuth2 / OIDC
/// scope registration. Composes the existing <see cref="GranitOpenIddictScopeQueryDefinition"/>
/// + <see cref="OpenIddictScopeExportDefinition"/> into the unified manifest surface.
/// </summary>
public sealed class GranitOpenIddictScopeEntityDefinition : EntityDefinition<GranitOpenIddictScope>
{
    /// <inheritdoc />
    public override string Name => "Granit.OpenIddict.Scope";

    /// <inheritdoc />
    protected override void Configure(EntityDefinitionBuilder<GranitOpenIddictScope> builder) =>
        builder
            .DisplayKey("OpenIddict:Entity.Scope")
            .Icon("tag")
            .PermissionGroup("OpenIddict.Scopes")
            .DisplayProperty(s => s.DisplayName)
            .SubtitleProperty(s => s.Name)
            .Query<GranitOpenIddictScopeQueryDefinition>()
            .Export<OpenIddictScopeExportDefinition>()
            .Form("default", f => f
                .Section("identity", s => s
                    .Field(x => x.Name)
                    .Field(x => x.DisplayName)
                    .Field(x => x.Description)))
            .Detail("default", d => d.Section("overview", s => s.InheritsFromForm("default")));
}
