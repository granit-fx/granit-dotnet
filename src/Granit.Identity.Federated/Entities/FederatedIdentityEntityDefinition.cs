using Granit.Entities;
using Granit.Identity.Federated.Domain;
using Granit.Identity.Federated.Exports;
using Granit.Identity.Federated.Queries;

namespace Granit.Identity.Federated.Entities;

/// <summary>
/// Phase 2 EntityDefinition for <see cref="FederatedIdentity"/> — the local cache
/// projection of users authenticated against an external IdP. Composes the
/// existing <see cref="FederatedIdentityQueryDefinition"/> +
/// <see cref="FederatedIdentityExportDefinition"/> into the unified manifest
/// surface.
/// </summary>
/// <remarks>
/// This is the federated-side counterpart of the local user record. The unified
/// User-aggregate work tracked separately (User-data centralization analysis)
/// will eventually unify both surfaces; until then, the federated cache is
/// admin-visible on its own.
/// </remarks>
public sealed class FederatedIdentityEntityDefinition : EntityDefinition<FederatedIdentity>
{
    /// <inheritdoc />
    public override string Name => "Granit.Identity.Federated.FederatedIdentity";

    /// <inheritdoc />
    protected override void Configure(EntityDefinitionBuilder<FederatedIdentity> builder) =>
        builder
            .DisplayKey("Identity.Federated:Entity.FederatedIdentity")
            .Icon("user")
            .PermissionGroup("Identity.Federated.UserCache")
            .DisplayProperty(u => u.Username)
            .SubtitleProperty(u => u.Email)
            .Query<FederatedIdentityQueryDefinition>()
            .Export<FederatedIdentityExportDefinition>()
            .Form("default", f => f
                .Section("identity", s => s
                    .Field(u => u.ExternalUserId, x => x.ReadOnly())
                    .Field(u => u.Username)
                    .Field(u => u.Email)
                    .Field(u => u.FirstName)
                    .Field(u => u.LastName)
                    .Field(u => u.Enabled))
                .Section("sync", s => s
                    .Field(u => u.LastSyncedAt, x => x.ReadOnly())))
            .Detail("default", d =>
            {
                d.Section("overview", s => s.InheritsFromForm("default"));
                d.SidePanel.Audit().Timeline();
            });
}
