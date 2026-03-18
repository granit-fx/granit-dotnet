using Granit.Authorization.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace Granit.Authorization.EntityFrameworkCore.DbContext;

/// <summary>
/// Implement this interface on the host application's <see cref="Microsoft.EntityFrameworkCore.DbContext"/>
/// to enable Granit.Authorization.EntityFrameworkCore persistence.
/// Call <see cref="PermissionGrantModelBuilderExtensions.ConfigureAuthorizationModule"/> in <c>OnModelCreating</c>.
/// </summary>
public interface IPermissionGrantDbContext
{
    /// <summary>Permission grants table. Configured with a unique index on (TenantId, Name, RoleName).</summary>
    DbSet<PermissionGrant> PermissionGrants { get; }
}
