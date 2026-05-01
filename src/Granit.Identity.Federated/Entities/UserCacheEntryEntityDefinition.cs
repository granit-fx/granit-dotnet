using Granit.Entities;
using Granit.Identity.Federated.Domain;
using Granit.Identity.Federated.Exports;
using Granit.Identity.Federated.Queries;

namespace Granit.Identity.Federated.Entities;

/// <summary>
/// Phase 2 EntityDefinition for <see cref="UserCacheEntry"/> — the local cache
/// projection of users authenticated against an external IdP. Composes the
/// existing <see cref="UserCacheEntryQueryDefinition"/> +
/// <see cref="UserCacheEntryExportDefinition"/> into the unified manifest
/// surface.
/// </summary>
/// <remarks>
/// This is the federated-side counterpart of the local user record. The unified
/// User-aggregate work tracked separately (User-data centralization analysis)
/// will eventually unify both surfaces; until then, the federated cache is
/// admin-visible on its own.
/// </remarks>
public sealed class UserCacheEntryEntityDefinition : EntityDefinition<UserCacheEntry>
{
    /// <inheritdoc />
    public override string Name => "Granit.Identity.Federated.UserCacheEntry";

    /// <inheritdoc />
    protected override void Configure(EntityDefinitionBuilder<UserCacheEntry> builder) =>
        builder
            .DisplayKey("Identity.Federated:Entity.UserCacheEntry")
            .Icon("user")
            .PermissionGroup("Identity.Federated.UserCache")
            .DisplayProperty(u => u.Username)
            .SubtitleProperty(u => u.Email)
            .Query<UserCacheEntryQueryDefinition>()
            .Export<UserCacheEntryExportDefinition>()
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
