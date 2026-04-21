using Granit.Authorization.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Authorization.EntityFrameworkCore.DbContext;

/// <summary>
/// Implement this interface on the host application's <see cref="Microsoft.EntityFrameworkCore.DbContext"/>
/// to enable Granit.Authorization.EntityFrameworkCore persistence.
/// Call <see cref="PermissionGrantModelBuilderExtensions.ConfigureAuthorizationModule"/> in <c>OnModelCreating</c>.
/// </summary>
public interface IPermissionGrantDbContext
{
    /// <summary>Permission grants table. Configured with a unique index on (TenantId, ProviderName, ProviderKey, Name).</summary>
    DbSet<PermissionGrant> PermissionGrants { get; }

    /// <summary>
    /// Role metadata table — declarative scope (<see cref="Granit.MultiTenancy.MultiTenancySide"/>,
    /// optional tenant / OIDC client) for roles managed via <see cref="IRoleMetadataStore"/>.
    /// </summary>
    /// <remarks>
    /// Default interface implementation resolves the set via the containing
    /// <see cref="Microsoft.EntityFrameworkCore.DbContext"/>, so existing host contexts that
    /// only declared <see cref="PermissionGrants"/> continue to compile. Override only if
    /// the set is exposed under a different property or a non-standard configuration.
    /// </remarks>
    DbSet<RoleMetadata> RoleMetadata => ((Microsoft.EntityFrameworkCore.DbContext)this).Set<RoleMetadata>();
}
