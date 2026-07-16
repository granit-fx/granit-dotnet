using Granit.DataFiltering;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Local.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated DbContext for the local-identity tables (users, roles, claims, logins, tokens, passkeys,
/// groups), built on <see cref="GranitDbContext"/>.
/// </summary>
/// <remarks>
/// The model is declared self-contained by
/// <see cref="IdentityLocalModelBuilderExtensions.ConfigureGranitIdentityLocal"/> — no
/// <c>IdentityDbContext</c> base. Owns a disjoint set of tables from the sibling
/// <c>OpenIddictDbContext</c> (which owns the OpenIddict + signing-key tables in the same database),
/// so the two isolated contexts never collide.
/// </remarks>
internal sealed class IdentityLocalDbContext(
    DbContextOptions<IdentityLocalDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null,
    IOptions<MetadataMappingOptions<LocalIdentity>>? extensionOptions = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    /// <summary>Gets the user groups set.</summary>
    public DbSet<GranitUserGroup> UserGroups => Set<GranitUserGroup>();

    /// <summary>Gets the user group members set.</summary>
    public DbSet<GranitUserGroupMember> UserGroupMembers => Set<GranitUserGroupMember>();

    /// <summary>
    /// A null-tenant user or group is global — visible under every tenant scope, preserving the
    /// pre-split consolidated behaviour.
    /// </summary>
    protected override bool TenantFilterTreatsNullAsGlobal => true;

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ConfigureGranitIdentityLocal(DataFilter, extensionOptions?.Value);
    }
}
