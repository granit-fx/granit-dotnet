using Granit.Core.DataFiltering;
using Granit.Core.MultiTenancy;
using Granit.OpenIddict.Domain;
using Granit.OpenIddict.Entities;
using Granit.OpenIddict.Entities.OpenIddict;
using Granit.OpenIddict.EntityFrameworkCore.Extensions;
using Granit.Persistence.Extensions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.OpenIddict.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated DbContext for the OpenIddict module, extending
/// <see cref="IdentityDbContext{TUser,TRole,TKey}"/> with OpenIddict entity support.
/// </summary>
/// <remarks>
/// Follows the canonical Granit isolated DbContext pattern
/// (cf. <c>ApiKeysDbContext</c>). Uses custom multi-tenant OpenIddict entities
/// (<see cref="GranitOpenIddictApplication"/>, etc.) instead of the defaults.
/// </remarks>
internal sealed class OpenIddictDbContext(
    DbContextOptions<OpenIddictDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : IdentityDbContext<GranitUser, GranitRole, Guid>(options)
{
    /// <summary>Gets the user groups set.</summary>
    public DbSet<GranitUserGroup> UserGroups => Set<GranitUserGroup>();

    /// <summary>Gets the user group members set.</summary>
    public DbSet<GranitUserGroupMember> UserGroupMembers => Set<GranitUserGroupMember>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // 1. ASP.NET Identity conventions (default table names, keys, indexes)
        base.OnModelCreating(modelBuilder);

        // 2. OpenIddict conventions with custom multi-tenant entities
        modelBuilder.UseOpenIddict<GranitOpenIddictApplication, GranitOpenIddictAuthorization,
            GranitOpenIddictScope, GranitOpenIddictToken, Guid>();

        // 3. Granit OpenIddict conventions (oidc_* table prefix, column constraints, manual filters)
        modelBuilder.ConfigureOpenIddictModule(dataFilter);

        // 4. Granit cross-cutting conventions (IMultiTenant, ISoftDeletable, IActive, etc.)
        // This automatically adds multi-tenant filters for IMultiTenant entities
        // (GranitOpenIddictApplication, GranitOpenIddictAuthorization, etc.)
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
