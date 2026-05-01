using Granit.Entities;
using Granit.Identity.Domain;
using Granit.Identity.Exports;
using Granit.Identity.Queries;

namespace Granit.Identity.Entities;

/// <summary>
/// Canonical <see cref="User"/> aggregate's UI surface (ADR-051 / ADR-040).
/// Composes the existing <see cref="UserQueryDefinition"/> +
/// <see cref="UserExportDefinition"/> into the manifest payload so admin
/// grids, OData consumers, and BI exports share one declaration.
/// </summary>
/// <remarks>
/// <para>
/// Permission group: <c>"Identity.Users"</c>. Permission constants are
/// declared by <c>Granit.Identity.Endpoints</c> per the framework
/// convention (<c>{Group}.{Resource}.Read|Manage|Delete</c>).
/// </para>
/// <para>
/// The form intentionally omits auth-secret and security-counter fields
/// (<c>PasswordHash</c>, <c>SecurityStamp</c>, <c>LockoutEnd</c>,
/// <c>AccessFailedCount</c>, <c>TwoFactorEnabled</c>): those live on
/// <c>LocalIdentity</c> and surface through a separate, narrower form
/// gated by an admin-only permission (B-step 2).
/// </para>
/// </remarks>
public sealed class UserEntityDefinition : EntityDefinition<User>
{
    /// <inheritdoc />
    public override string Name => "Granit.Identity.User";

    /// <inheritdoc />
    protected override void Configure(EntityDefinitionBuilder<User> builder) =>
        builder
            .DisplayKey("Identity:Entity.User")
            .Icon("user")
            .PermissionGroup("Identity.Users")
            .DisplayProperty(u => u.DisplayName)
            .SubtitleProperty(u => u.Email)
            .Query<UserQueryDefinition>()
            .Export<UserExportDefinition>()
            .Form("default", f => f
                .Section("identity", s => s
                    .Field(u => u.DisplayName)
                    .Field(u => u.Email)
                    .Field(u => u.FirstName)
                    .Field(u => u.LastName)
                    .Field(u => u.PhoneNumber)
                    .Field(u => u.IsEnabled))
                .Section("locale", s => s
                    .Field(u => u.PreferredLocale)
                    .Field(u => u.Timezone)))
            .Form("quick", f => f
                .Section("essentials", s => s
                    .Field(u => u.DisplayName)
                    .Field(u => u.Email)))
            .Detail("default", d =>
            {
                d.Section("overview", s => s.InheritsFromForm("default"));
                d.SidePanel.Audit().Timeline();
            });
}
