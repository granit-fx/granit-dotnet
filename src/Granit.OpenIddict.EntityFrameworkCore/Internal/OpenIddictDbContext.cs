using Granit.DataFiltering;
using Granit.Identity.Local.Domain;
using Granit.MultiTenancy;
using Granit.OpenIddict.Domain;
using Granit.OpenIddict.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Granit.OpenIddict.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated DbContext for the OpenIddict module and its co-located ASP.NET Core Identity tables.
/// </summary>
/// <remarks>
/// <para>
/// Built on <see cref="GranitDbContext"/> — the parameterised <c>IMultiTenant</c> filter and
/// <c>ApplyGranitConventions</c> come from the base, so the reflection-based filter replication a
/// non-<c>GranitDbContext</c> derivative would need is gone.
/// </para>
/// <para>
/// The context does <b>not</b> inherit <c>IdentityDbContext</c>: the entire model — the Identity
/// tables (users/roles/claims/logins/tokens/passkeys), their keys, unique indexes
/// (<c>UserNameIndex</c>/<c>EmailIndex</c>/<c>RoleNameIndex</c>), maxlengths and relationships, plus
/// the OpenIddict entities, group tables and signing keys — is defined self-contained by
/// <see cref="OpenIddictModelBuilderExtensions.ConfigureOpenIddictModule"/>. A relational-model
/// pinning test guards that this produces a byte-identical schema.
/// </para>
/// </remarks>
internal sealed class OpenIddictDbContext(
    DbContextOptions<OpenIddictDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null,
    IOptions<MetadataMappingOptions<LocalIdentity>>? extensionOptions = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    /// <summary>Gets the user groups set.</summary>
    public DbSet<GranitUserGroup> UserGroups => Set<GranitUserGroup>();

    /// <summary>Gets the user group members set.</summary>
    public DbSet<GranitUserGroupMember> UserGroupMembers => Set<GranitUserGroupMember>();

    /// <summary>Gets the signing keys set.</summary>
    public DbSet<SigningKey> SigningKeys => Set<SigningKey>();

    /// <summary>
    /// OpenIddict's null-tenant applications, scopes and tokens are global — visible under every
    /// tenant scope (the ClientId already carries the tenant, so isolation holds via app + subject).
    /// </summary>
    protected override bool TenantFilterTreatsNullAsGlobal => true;

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Self-contained model: Identity + OpenIddict entities, the openiddict_* prefix, column
        // constraints and the manual LocalIdentity soft-delete filter. The IMultiTenant filter and
        // ApplyGranitConventions run in GranitDbContext.OnModelCreating after this override.
        modelBuilder.ConfigureOpenIddictModule(DataFilter, extensionOptions?.Value);
    }
}
