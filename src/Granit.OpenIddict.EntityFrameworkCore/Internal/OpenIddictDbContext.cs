using Granit.DataFiltering;
using Granit.Identity.Local.Domain;
using Granit.MultiTenancy;
using Granit.OpenIddict.Domain;
using Granit.OpenIddict.Entities.OpenIddict;
using Granit.OpenIddict.EntityFrameworkCore.Extensions;
using Granit.Persistence.Extensions;
using Granit.Persistence.ExtraProperties;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Granit.OpenIddict.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated DbContext for the OpenIddict module, extending
/// <see cref="IdentityDbContext{TUser,TRole,TKey}"/> with OpenIddict entity support.
/// </summary>
/// <remarks>
/// Follows the canonical Granit isolated DbContext pattern
/// (cf. <c>AuthenticationApiKeysDbContext</c>). Uses custom multi-tenant OpenIddict entities
/// (<see cref="GranitOpenIddictApplication"/>, etc.) instead of the defaults.
/// </remarks>
internal sealed class OpenIddictDbContext(
    DbContextOptions<OpenIddictDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null,
    IOptions<ExtraPropertyMappingOptions<GranitUser>>? extensionOptions = null)
    : IdentityDbContext<GranitUser, GranitRole, Guid>(options)
{
    /// <summary>Gets the user groups set.</summary>
    public DbSet<GranitUserGroup> UserGroups => Set<GranitUserGroup>();

    /// <summary>Gets the user group members set.</summary>
    public DbSet<GranitUserGroupMember> UserGroupMembers => Set<GranitUserGroupMember>();

    /// <summary>Gets the signing keys set.</summary>
    public DbSet<SigningKey> SigningKeys => Set<SigningKey>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // 1. ASP.NET Identity conventions (default table names, keys, indexes)
        base.OnModelCreating(builder);

        // 2. OpenIddict conventions with custom multi-tenant entities
        builder.UseOpenIddict<GranitOpenIddictApplication, GranitOpenIddictAuthorization,
            GranitOpenIddictScope, GranitOpenIddictToken, Guid>();

        // 3. Granit OpenIddict conventions (openiddict_* table prefix, column constraints, manual filters)
        builder.ConfigureOpenIddictModule(dataFilter, extensionOptions?.Value);

        // 4. Granit cross-cutting conventions (IMultiTenant, ISoftDeletable, IActive, etc.)
        // This automatically adds multi-tenant filters for IMultiTenant entities
        // (GranitOpenIddictApplication, GranitOpenIddictAuthorization, etc.)
        builder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
